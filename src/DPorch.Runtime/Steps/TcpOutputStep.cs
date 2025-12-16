using System.Text.Json;
using System.Threading.Channels;
using DPorch.Logging;
using DPorch.Runtime.Networking;
using DPorch.Steps;
using NetMQ;
using NetMQ.Sockets;

namespace DPorch.Runtime.Steps;

/// <summary>
///     Sends serialized data to target pipelines over TCP using NetMQ PushSocket and UDP discovery.
/// </summary>
public class TcpOutputStep(string pipelineName, string[] outTargPipes, int discoveryPort, ILogger log) : IOutputStep
{
    const int JoinTimeoutSeconds = 10;

    readonly CancellationTokenSource _cts = new();
    readonly Channel<byte[]> _sendQueue = GetQueueInitializer();
    Thread? _senderThread;
    Exception? _senderThreadEx;

    /// <inheritdoc />
    public CancellationToken? StepCancellationToken { get; set; }

    /// <inheritdoc />
    public void Awake()
    {
        if (StepCancellationToken == null)
            throw new NullReferenceException("Null cancellation token in TCP output module Awake()");
        
        // Uses Guid as network identity to avoid pipeline name collisions in a TcpInputModule instance
        var guid = Guid.NewGuid();
        var ackReq = new InputSourcePipelineInfo(pipelineName, guid);
        var ackReqBytes = JsonSerializer.SerializeToUtf8Bytes(ackReq);
        var finder = new UdpFinder(ackReqBytes, outTargPipes, discoveryPort, log, StepCancellationToken.Value);

        // A list will be generated containing all target pipeline UCRI
        var targUriList = finder.DiscoverAsync().GetAwaiter().GetResult();

        if (targUriList.Count != outTargPipes.Length)
        {
            log.Error("Failed to connect to {Count} pipelines", outTargPipes.Length);
            throw new InvalidOperationException("TCP output module failed to discover all target pipelines");
        }

        var sock = new PushSocket();

        foreach (var uri in targUriList)
        {
            sock.Connect(uri);
            log.Debug("TCP output module connected to target pipeline at {Uri}", uri);
        }

        _senderThread = new Thread(() =>
            SendInBackground(guid.ToByteArray(), targUriList, sock).GetAwaiter().GetResult());

        _senderThread.IsBackground = true;
        _senderThread.Start();
    }

    /// <inheritdoc />
    public void Send(byte[]? dataBuffer)
    {
        if (dataBuffer == null)
            return;

        if (_senderThreadEx != null)
            throw new AggregateException($"TCP output module thread caught an exception: {_senderThreadEx}");

        if (_sendQueue.Writer.TryWrite(dataBuffer))
            return;

        throw new InvalidOperationException(
            $"TCP output module failed to enqueue {dataBuffer.Length} byte data packet");
    }

    /// <inheritdoc />
    public void End()
    {
        _sendQueue.Writer.Complete();

        _cts.Cancel();
        _senderThread?.Join(TimeSpan.FromSeconds(JoinTimeoutSeconds));
        _cts.Dispose();
        _senderThread = null;
    }

    async Task SendInBackground(byte[] guidBuffer, IReadOnlyList<string> targUriList, PushSocket sock)
    {
        try
        {
            log.Debug("TCP output module thread started");

            if (StepCancellationToken == null)
                throw new NullReferenceException("Null cancellation token in TCP output module thread");

            // Wait for new serialized output data, pair data with Guid, and send to all target pipelines     
            while (await _sendQueue.Reader.WaitToReadAsync(StepCancellationToken.Value))
            while (_sendQueue.Reader.TryRead(out var outputData))
                for (var i = 0; i < targUriList.Count; i++)
                {
                    // Message -> Guid (Frame 0), Output Data (Frame 1)
                    sock.SendMoreFrame(guidBuffer);
                    sock.SendFrame(outputData);
                }
        }
        catch (TaskCanceledException)
        {

        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception ex)
        {
            log.Error(ex, "TCP output module thread caught an exception: {Message}", ex.Message);
            _senderThreadEx = ex;
        }
        finally
        {
            foreach (var uri in targUriList)
                sock.Disconnect(uri);
            sock.Close();
            sock.Dispose();
            log.Debug("TCP output module thread stopped");
        }
    }

    static Channel<byte[]> GetQueueInitializer()
    {
        return Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true, // background thread
            SingleWriter = true // Send(byte[]) called from pipeline provides serialized data
        });
    }
}