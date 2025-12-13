namespace DPorch.Runtime.Networking;

/// <summary>
///     Contains discovery information broadcast by a <see cref="UdpBeacon" />.
/// </summary>
/// <param name="Name"> Beacon's advertised name. </param>
/// <param name="ListenerPort"> TCP port where the beacon accepts connections. </param>
public readonly record struct BeaconInfo(string Name, int ListenerPort);