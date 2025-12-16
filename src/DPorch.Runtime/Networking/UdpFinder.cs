using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DPorch.Logging;

namespace DPorch.Runtime.Networking;

/// <summary>
///     Discovers UDP beacons on the local network and collects their acknowledgement messages.
/// </summary>
/// <remarks>
///     <para>
///         Finders are used by pipelines to obtain their target's URI to send data to. Each target pipeline will
///         use a beacon to announce its presence and listener port for TCP connections. The finder listens for these beacons
///         and connects to each target's TCP listener to request an acknowledgement message containing the target's
///         unique connection URI. In exchange the finder will provide this pipeline's name and Guid to the beacon(s).
///     </para>
/// </remarks>
/// <param name="ackReqMsg">
///     Message sent beacons when requesting their acknowledgement. This contains the finder's pipeline name and Guid.
/// </param>
/// <param name="targNames">
///     Names of target beacons to discover on the network. These would be the name of the target pipelines themselves.
/// </param>
/// <param name="discoveryPort">
///     UDP port where beacons are broadcasting their presence.
/// </param>
/// <param name="log">
///     Logger for providing finder status updates.
/// </param>
public class UdpFinder(byte[] ackReqMsg, string[] targNames, int discoveryPort, ILogger log, CancellationToken stepCancelTkn) : IDisposable
{
    const int AcknowledgementBufferSize = 1024;
    const int TimeoutMinutes = 10;
    
    readonly List<string> _ackMsgs = [];
    
    readonly UdpClient _udpClient = new();

    bool _isDisposed;
    
    /// <summary>
    ///     Listens for UDP beacon broadcasts and collects acknowledgement messages from all target beacons.
    /// </summary>
    /// <returns>
    ///     Acknowledgement messages received from all target beacons. Contains exactly one message per target
    ///     unless timeout or cancellation occurs.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         Binds to the discovery port and listens for beacon broadcasts. For each target, waits until a
    ///         matching beacon is found, then connects via TCP to exchange identification and receive the
    ///         acknowledgement message.
    ///     </para>
    ///     <para>
    ///         Targets are discovered sequentially. The finder is automatically disposed after completion.
    ///     </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if <see cref="Dispose" /> or <see cref="DiscoverAsync" /> were already called.
    /// </exception>
    public async Task<IReadOnlyList<string>> DiscoverAsync()
    {
        if (_isDisposed)
            throw new InvalidOperationException(nameof(DiscoverAsync));

        try
        {
            var udpEp = new IPEndPoint(IPAddress.Any, discoveryPort);

            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(udpEp);
            log.Debug("Listening for beacons on UDP port {DiscoverPort}", discoveryPort);

            // Don't search in parallel because all beacons are required to send a message regardless
            // Time added by synchronous target discovery is negligible compared to timeout durations
            // (All of this assuming both UdpFinders and UdpBeacons are configured correctly)
            foreach (var targName in targNames)
            {
                if (stepCancelTkn.IsCancellationRequested)
                    return [];

                await DiscoverTarget(targName);
                log.Debug("Received acknowledgement from UDP beacon {Name}: {AckMsg}", targName, _ackMsgs.Last());
            }

            return _ackMsgs.AsReadOnly();
        }
        catch (OperationCanceledException)
        {
            throw; // Cancellation requested by client, abort without logging error (expected behavior)
        }
        catch (Exception ex)
        {
            log.Error(ex, "UDP finder error: {Message}", ex.Message);
            throw;
        }
        finally
        {
            Dispose();
        }
    }

    async Task DiscoverTarget(string targName)
    {
        while (!stepCancelTkn.IsCancellationRequested)
        {
            var result = await _udpClient.ReceiveAsync(stepCancelTkn);

            // Expecting JSON containing name of beacon & port of its TCPListener (serialized BeaconInfo)
            var rawUdpMsg = Encoding.UTF8.GetString(result.Buffer);
            var deserializedMsg = JsonSerializer.Deserialize<BeaconInfo>(rawUdpMsg);

            if (deserializedMsg.Name != targName)
                continue;

            await RequestAcknowledgement(result.RemoteEndPoint.Address, deserializedMsg.ListenerPort);

            return;
        }
    }

    async Task RequestAcknowledgement(IPAddress beaconAddr, int listenerPort)
    {
        using var msgSenderClient = new TcpClient();
        await msgSenderClient.ConnectAsync(beaconAddr, listenerPort, stepCancelTkn);

        using var stream = msgSenderClient.GetStream();

        // Send listener the name of this pipeline
        await stream.WriteAsync(ackReqMsg, 0, ackReqMsg.Length);

        var buffer = new byte[AcknowledgementBufferSize];
        var bytesRead = await stream.ReadAsync(buffer, stepCancelTkn);
        _ackMsgs.Add(Encoding.UTF8.GetString(buffer, 0, bytesRead));
    }
    
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _udpClient.Client.Close();
        _udpClient.Client.Dispose();
        _udpClient.Close();
        _udpClient.Dispose();
    }
}