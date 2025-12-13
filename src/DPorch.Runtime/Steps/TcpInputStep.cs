using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DPorch.Logging;
using DPorch.Runtime.Networking;
using DPorch.Steps;
using NetMQ;
using NetMQ.Sockets;

namespace DPorch.Runtime.Steps;

/// <summary>
///     Receives serialized data from source pipelines over TCP using NetMQ PullSocket and UDP discovery.
/// </summary>
public class TcpInputStep(
    string pipeName,
    int srcPipesCount,
    int discoverPort,
    string inNetIface,
    string[] outNetIfaces,
    ILogger log) : IInputStep
{
    const string TcpProtocol = "tcp://";
    const int ExpectedMessageFrameCount = 2;

    // TCP latency seems to be about 2ms on a local network and < 1ms on localhost
    const int StepThreadSleepMs = 1;
    const int BackgroundThreadSleepMs = 1;
    const int JoinTimeoutSeconds = 3;
    readonly PullSocket _sock = new();
    readonly TaskCompletionSource _tcs = new();
    readonly Lock _tcsLock = new();


    NetworkMessageContainer? _msgContainer;
    Thread? _receiverThread;

    /// <inheritdoc />
    public CancellationToken? StepCancellationToken { get; set; }

    /// <inheritdoc />
    public void Awake()
    {
        var sockAddr = TcpProtocol + GetIpAddress();

        // Note: This socket will accept messages even during the discovery phase to be processed later
        var sockPort = _sock.BindRandomPort(sockAddr);
        var sockUri = $"{sockAddr}:{sockPort}";
        log.Debug("TCP input module socket bound to {URI}", sockUri);

        // TCP URI will be shared with input source pipelines when the UDP acknowledges them
        var sockUriBytes = Encoding.UTF8.GetBytes(sockUri);

        // Stop broadcasting as soon as enough unique input source pipelines have sent messages
        using var beacon = new UdpBeacon(
            pipeName, 
            sockUriBytes, 
            srcPipesCount, 
            discoverPort, 
            outNetIfaces, 
            StepCancellationToken ?? throw new NullReferenceException("Null cancellation token in TCP input step"), 
            log);
        
        var connInfoList = beacon.DiscoverAsync().GetAwaiter().GetResult();
        var connCount = connInfoList?.Count ?? 0;
        
        if (StepCancellationToken.Value.IsCancellationRequested)
            throw new OperationCanceledException("TCP input step discovery operation was cancelled");

        if (connInfoList == null || connCount != srcPipesCount)
            throw new InvalidOperationException(
                $"TCP input module only received {connCount}/{srcPipesCount} of the required connections");

        log.Debug("TCP input module received {Count} connections", connCount);

        // Create a container that has queues for each source pipeline
        _msgContainer = new NetworkMessageContainer(connInfoList.Select(j =>
            JsonSerializer.Deserialize<InputSourcePipelineInfo>(j)).ToList());

        // Begin moving any incoming messages to the container in the background
        _receiverThread = new Thread(() => ReceiveInBackground(_sock))
        {
            IsBackground = true
        };

        _receiverThread.Start();
        log.Info("All {Count} source pipelines connected", srcPipesCount);
    }

    /// <inheritdoc />
    public Dictionary<string, byte[]> Receive()
    {
        if (_msgContainer == null)
            throw new InvalidOperationException("Null message container in TCP input step");

        if (StepCancellationToken == null)
            throw new NullReferenceException("Null cancellation token in TCP input step");

        // Block until at least one message is available from each input source pipeline
        while (!_msgContainer.IsMessageForEachInputSource())
        {
            // Seeing as this is the very beginning of an iteration and may be waiting indefinitely, throw if canceled
            if (StepCancellationToken.Value.IsCancellationRequested)
                throw new OperationCanceledException("TCP input step receive operation was cancelled");

            lock (_tcsLock)
                if (_tcs.Task.IsFaulted)
                    throw new AggregateException("Error occurred on background TCP input thread", _tcs.Task.Exception);

            Thread.Sleep(StepThreadSleepMs);
        }

        return _msgContainer.GetStepMessageMap();
    }

    /// <inheritdoc />
    public void End()
    {
        // If the client made a cancellation request during Awake() and the thread never started
        if (_receiverThread == null)
            return;
        
        // The socket has already been closed and disposed of in the background thread
        lock (_tcsLock)
            if (_tcs.Task.IsCompletedSuccessfully)
                return;

        // Log forceful thread joins without throwing an exception
        // While not ideal, a forceful join doesn't leave the object in an invalid state at this point
        if (_receiverThread.Join(TimeSpan.FromSeconds(JoinTimeoutSeconds)))
        {
            log.Debug("TCP input thread joined successfully");
            return;
        }

        log.Warn("TCP input thread forcefully joined");

        // Lock is no longer needed, but just for consistency and safety
        lock (_tcsLock)
            if (_tcs.Task.IsFaulted)
                log.Warn("TCP input thread error on exit: {Ex}", _tcs.Task.Exception.Flatten().Message);
    }

    void ReceiveInBackground(PullSocket sock)
    {
        try
        {
            log.Debug("TCP input module background receive thread started");

            if (sock.IsDisposed)
                throw new ObjectDisposedException("TCP input socket was disposed before background thread started");

            if (StepCancellationToken == null)
                throw new NullReferenceException("Null cancellation token in TCP input thread");

            if (_msgContainer == null)
                throw new NullReferenceException("Null message container in TCP input thread");

            // Reuse frame list to avoid excessive garbage collection
            var frames = new List<byte[]>();

            while (!StepCancellationToken.Value.IsCancellationRequested)
            {
                // Do not block WHILE checking for bytes, but briefly block if none are available
                if (!sock.TryReceiveMultipartBytes(ref frames, ExpectedMessageFrameCount))
                {
                    Thread.Sleep(BackgroundThreadSleepMs);
                    continue;
                }

                // Message -> Guid (Frame 0), Output Data (Frame 1)
                var guid = new Guid(frames.First());

                // Enqueue message mapped to the input source pipeline's Guid
                _msgContainer.Enqueue(guid, frames.Last());
                frames.Clear();
            }

            lock (_tcsLock)
                _tcs.SetResult();
        }
        catch (TaskCanceledException)
        {
            lock (_tcsLock)
                _tcs.SetResult();
        }
        catch (OperationCanceledException)
        {
            lock (_tcsLock)
                _tcs.SetResult();
        }
        catch (Exception ex)
        {
            // This will be detected by Get(), rethrown there, and logged by an upper layer
            log.Debug("TCP input module background receiver thread encountered an exception: {Ex}", ex.Message);

            lock (_tcsLock)
                _tcs.SetException(ex);
        }
        finally
        {
            sock.Dispose();
        }
    }

    IPAddress GetIpAddress()
    {
        var networkInterface = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(ni => ni.Name == inNetIface);

        if (networkInterface == null)
            throw new InvalidOperationException($"Network interface '{inNetIface}' not found");

        var ipv4Address = networkInterface.GetIPProperties().UnicastAddresses.FirstOrDefault(ip =>
            ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address));

        if (ipv4Address == null)
            throw new InvalidOperationException($"No IPv4 address found on interface '{inNetIface}'");

        return ipv4Address.Address;
    }
}