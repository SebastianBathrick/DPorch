using System.Collections.Immutable;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DPorch.Logging;

namespace DPorch.Runtime.Networking;

// TODO: Add better timeout logic
/// <summary>
///     Broadcasts UDP beacon messages and listens for TCP connection requests from <see cref="UdpFinder" />
///     instances until the required number of finders connect or cancellation is requested.
/// </summary>
/// <param name="beaconName"> The unique identifier for this beacon used in discovery messages. </param>
/// <param name="ackMsg"> The acknowledgement message sent back to finders upon successful connection. </param>
/// <param name="reqFinderCount">
///     The required number of unique finder connections to collect before completing discovery.
/// </param>
/// <param name="discoveryPort"> The UDP port used for broadcasting beacon discovery messages. </param>
/// <param name="outNetIfaces"> The network interface names to use for broadcasting. </param>
/// <param name="stepCancelTkn"> Cancellation token to stop discovery operations. </param>
/// <param name="log"> The logger used for diagnostic output. </param>
public class UdpBeacon(
    string beaconName,
    byte[] ackMsg,
    int reqFinderCount,
    int discoveryPort,
    string[] outNetIfaces,
    CancellationToken stepCancelTkn,
    ILogger log) : IDisposable
{
    #region Constants

    const int ConnectionStreamBufferSize = 1024;
    const int UdpMessageIntervalMs = 250;
    const int TimeoutMinutes = 10;

    #endregion

    #region Fields

    readonly string _beaconName = beaconName;
    readonly int _reqFinderCount = reqFinderCount;
    readonly byte[] _ackMsg = ackMsg;
    readonly Dictionary<string, string> _finderMsgMap = [];
    Exception? _loopTaskEx;
    bool _isDisposed;

    #endregion

    #region Client API
    
    /// <summary>
    ///     Broadcasts UDP messages and collects messages from <see cref="UdpFinder" /> instances until
    ///     the required number of unique connections is reached or cancellation is requested.
    /// </summary>
    /// <returns>
    ///     Messages received from unique finders. Contains exactly <c>reqFinderCount</c> messages
    ///     on success, or fewer if cancelled.
    /// </returns>
    /// <remarks>
    ///     Not thread-safe - call once per instance. The beacon is automatically disposed after completion.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when beacon is already disposed or when duplicate finder connections are detected.
    /// </exception>
    /// <exception cref="TaskCanceledException">
    ///     Thrown when cancellation is requested via the cancellation token.
    /// </exception>
    /// <exception cref="AggregateException">
    ///     Thrown when errors occur in both the discovery loop and background messaging thread.
    /// </exception>
    public async Task<IReadOnlyList<string>> DiscoverAsync()
    {
        if (_isDisposed)
            throw new InvalidOperationException("UDP Beacon disposed");

        if (stepCancelTkn.IsCancellationRequested)
            throw new TaskCanceledException("UDP Beacon discovery cancelled before starting");
        
        Task? loopTask = null;
        Exception? thisFuncEx = null;
        var cts = new CancellationTokenSource();

        try
        {
            /* The UDP clients send messages on a select number of network interfaces while the TCP listener listens for client
             activity on all interfaces.*/
            using var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();

            // Start sending messages on network interfaces with the listener's port
            var listenerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            var beaconInfoBytes = GetBeaconInfoBytes(_beaconName, listenerPort);
            loopTask = Task.Run(() => UdpMessagingLoop(_beaconName, beaconInfoBytes, cts.Token));

            log.Debug(
                "UDP beacon '{BeaconName}' started broadcasting on port {DiscoveryPort} " +
                "and accepting connections on port {ListenerPort}", _beaconName, discoveryPort, listenerPort);

            // While unique connection count has not been reached and  
            while (_finderMsgMap.Count < _reqFinderCount)
            {
                if (_loopTaskEx != null)
                    throw new AggregateException("Error occurred in UDP messaging loop", _loopTaskEx);
                
                if (stepCancelTkn.IsCancellationRequested)
                    throw new TaskCanceledException("UDP Beacon discovery cancelled during operation");

                // Get the finder TCP client object that connected to the listener
                // This will throw a TaskCanceledException if there's a cancellation is requested
                var (finderClient, finderEp) = await GetFinderTcpClient(listener);

                if (finderClient != null && finderEp != null)
                {
                    // Get message sent by finder and then send an acknowledgement to it
                    var finderMsg = await GetUdpFinderMessage(finderClient);

                    log.Trace("Received message from finder at {Endpoint}: {FinderMsg}", finderEp, finderMsg);

                    // If finder already mapped an error occurred and user's app won't function properly
                    if (!_finderMsgMap.TryAdd(finderEp, finderMsg))
                        throw new InvalidOperationException($"Received message from pipeline with invalid state:  {finderMsg}");

                    log.Trace("Found by unique UDP finder sent message with socket bound to {Endpoint}", finderEp);
                }

                finderClient?.Close();
                finderClient?.Dispose();
            }

            // Stop broadcasting and listening once we have all connections
            listener.Stop();
        }
        catch (OperationCanceledException ex)
        {
            log.Trace("UDP beacon '{BeaconName}' discovery cancelled exception: {Message}", _beaconName, ex.Message);
        }
        catch (Exception ex)
        {
            thisFuncEx = ex;
        }
        finally
        {
            cts.Cancel();
            
            // Wait until the loop on the thread pool stops and throw if it had an exception
            if (loopTask != null)
                await loopTask;

            Dispose();
            ThrowIfAggregateException(thisFuncEx);
        }

        return _finderMsgMap.Values.ToImmutableList();
    }

    async Task UdpMessagingLoop(string selfName, byte[] beaconInfoBytes, CancellationToken taskCancelTkn)
    {
        var clientEpPairs = new List<(UdpClient client, IPEndPoint ep)>();

        try
        {
            clientEpPairs = GetUdpClientsAndEndpoints();

            while (!stepCancelTkn.IsCancellationRequested && !taskCancelTkn.IsCancellationRequested)
            {
                foreach (var (client, ep) in clientEpPairs)
                    await client.SendAsync(beaconInfoBytes, beaconInfoBytes.Length, ep).WaitAsync(stepCancelTkn);

                await Task.Delay(UdpMessageIntervalMs, stepCancelTkn);
            }
        }
        // Thrown if cancellation occurs during Task.Delay or SendAsync so ignore
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            _loopTaskEx = ex;
        }
        finally
        {
            foreach (var (client, _) in clientEpPairs)
                client.Dispose();
        }
    }
    
    #endregion
    
    #region Get Finders Helpers

    static byte[] GetBeaconInfoBytes(string selfName, int ackPort)
    {
        var info = new BeaconInfo(selfName, ackPort);
        var json = JsonSerializer.Serialize(info);
        return Encoding.UTF8.GetBytes(json);
    }

    async Task<(TcpClient client, string? ep)> GetFinderTcpClient(TcpListener listener)
    {
        // TODO: Allow user preferences to define a timeout for pipeline discovery
        var client = await listener.AcceptTcpClientAsync(stepCancelTkn);

        if (stepCancelTkn.IsCancellationRequested)
            throw new TaskCanceledException("UDP Beacon discovery cancelled during TCP client acceptance");

        var ep = client.Client.RemoteEndPoint?.ToString();
        return (client, ep);
    }

    async Task<string> GetUdpFinderMessage(TcpClient finderClient)
    {
        await using var stream = finderClient.GetStream();

        var buffer = new byte[ConnectionStreamBufferSize];
        var bytesRead = await stream.ReadAsync(buffer, 0, ConnectionStreamBufferSize, stepCancelTkn);
        var finderMsg = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        // Send acknowledgement message with any vital/useful data if needed
        await stream.WriteAsync(_ackMsg, stepCancelTkn);
        return finderMsg;
    }

    void ThrowIfAggregateException(Exception? thisFuncEx)
    {
        var ex = new List<Exception>();

        if (thisFuncEx != null)
            ex.Add(thisFuncEx);
        if (_loopTaskEx != null)
            ex.Add(_loopTaskEx);

        if (ex.Count != 0)
            throw new AggregateException(nameof(DiscoverAsync), ex);
    }

    #endregion

    #region Messaging Loop Helpers

    List<(UdpClient client, IPEndPoint ep)> GetUdpClientsAndEndpoints()
    {
        var netInterfaces = GetOutInterfaces();
        var udpList = new List<(UdpClient client, IPEndPoint ep)>();


        foreach (var netInterface in netInterfaces)
        {
            // Skip interfaces that aren't operational
            if (netInterface.OperationalStatus != OperationalStatus.Up)
            {
                log.Warn("Skipping interface '{InterfaceName}' - not operational (status: {Status})", netInterface.Name,
                    netInterface.OperationalStatus);
                continue;
            }

            try
            {
                var ipv4 = GetIPv4Address(netInterface);
                var subnetMask = GetSubnetMask(netInterface);
                var broadcastIp = GetBroadcastIp(ipv4, subnetMask);

                var udpClient = new UdpClient();
                udpClient.Client.Bind(new IPEndPoint(ipv4, 0));
                udpClient.EnableBroadcast = true;

                var udpEndpoint = new IPEndPoint(broadcastIp, discoveryPort);
                udpList.Add((udpClient, udpEndpoint));
                log.Debug("Using interface '{InterfaceName}' with broadcast IP {BroadcastIp}", netInterface.Name,
                    broadcastIp);
            }
            catch (SocketException ex)
            {
                log.Warn("Skipping interface '{InterfaceName}' - failed to bind: {Message}", netInterface.Name,
                    ex.Message);
            }
        }

        if (udpList.Count == 0)
            throw new InvalidOperationException("No usable network interfaces found for UDP broadcast.");

        return udpList;
    }

    public NetworkInterface[] GetOutInterfaces()
    {
        return NetworkInterface.GetAllNetworkInterfaces().Where(ni => outNetIfaces.Contains(ni.Name)).ToArray();
    }

    static IPAddress GetSubnetMask(NetworkInterface netInterface)
    {
        var addrs = netInterface.GetIPProperties().UnicastAddresses
            .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork);

        if (addrs.Any())
            return addrs.First().IPv4Mask;

        throw new InvalidOperationException($"No IPv4 address found for interface: {netInterface.Name}");
    }

    static IPAddress GetIPv4Address(NetworkInterface @interface)
    {
        var ips = @interface.GetIPProperties().UnicastAddresses;

        foreach (var ip in ips)
            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                return ip.Address;

        throw new InvalidOperationException($"No IPv4 address found on interface '{@interface.Name}'");
    }

    static IPAddress GetBroadcastIp(IPAddress ipv4, IPAddress subnetMask)
    {
        var ip = ipv4.GetAddressBytes();
        var mask = subnetMask.GetAddressBytes();

        if (ip.Length != mask.Length)
            throw new InvalidOperationException("IP address and subnet mask length mismatch");

        // NOTE: This DOES support different subnet mask lengths (e.g., /24, /16)
        var broadcast = new byte[ip.Length];
        for (var i = 0; i < ip.Length; i++)
            broadcast[i] = (byte)(ip[i] | ~mask[i]);

        return new IPAddress(broadcast);
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
    }
    
    #endregion
}