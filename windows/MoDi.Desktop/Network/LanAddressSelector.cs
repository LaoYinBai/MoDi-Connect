using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MoDi.Desktop.Network;

internal readonly record struct LanAddressCandidate(
    IPAddress Address,
    bool IsUsable,
    bool HasDefaultGateway);

internal static class LanAddressSelector
{
    internal static IReadOnlyList<IPAddress> GetAdvertisedAddresses()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(CreateCandidates)
            .ToArray();

        return SelectAdvertisedAddresses(candidates, ProbeDefaultRouteAddress());
    }

    internal static IReadOnlyList<IPAddress> SelectAdvertisedAddresses(
        IEnumerable<LanAddressCandidate> candidates,
        IPAddress? defaultRouteAddress)
    {
        var usable = candidates
            .Where(candidate => candidate.IsUsable && IsRoutableIpv4(candidate.Address))
            .ToArray();

        if (defaultRouteAddress is not null)
        {
            var routed = usable.FirstOrDefault(candidate => candidate.Address.Equals(defaultRouteAddress));
            if (routed != default)
                return [routed.Address];
        }

        return usable
            .Where(candidate => candidate.HasDefaultGateway)
            .Select(candidate => candidate.Address)
            .Distinct()
            .ToArray();
    }

    private static IEnumerable<LanAddressCandidate> CreateCandidates(NetworkInterface networkInterface)
    {
        var properties = networkInterface.GetIPProperties();
        var hasDefaultGateway = properties.GatewayAddresses.Any(gateway =>
            gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
            !gateway.Address.Equals(IPAddress.Any));
        var usable = networkInterface.OperationalStatus == OperationalStatus.Up &&
                     networkInterface.SupportsMulticast &&
                     networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback and
                         not NetworkInterfaceType.Tunnel;

        return properties.UnicastAddresses
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => new LanAddressCandidate(address.Address, usable, hasDefaultGateway));
    }

    private static IPAddress? ProbeDefaultRouteAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(IPAddress.Parse("192.0.2.1"), 9);
            return (socket.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static bool IsRoutableIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork &&
               !IPAddress.IsLoopback(address) &&
               !address.Equals(IPAddress.Any) &&
               !address.Equals(IPAddress.Broadcast) &&
               !(bytes[0] == 169 && bytes[1] == 254);
    }
}
