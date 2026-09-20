using System.Net;
using MoDi.Desktop.Network;
using Xunit;

namespace MoDi.Desktop.Tests.Network;

public sealed class LanAddressSelectorTests
{
    [Fact]
    public void Selects_the_default_route_address_instead_of_virtual_adapter_addresses()
    {
        var addresses = new[]
        {
            new LanAddressCandidate(IPAddress.Parse("172.27.64.1"), IsUsable: true, HasDefaultGateway: false),
            new LanAddressCandidate(IPAddress.Parse("192.168.56.1"), IsUsable: true, HasDefaultGateway: false),
            new LanAddressCandidate(IPAddress.Parse("172.20.182.22"), IsUsable: true, HasDefaultGateway: true),
        };

        var selected = LanAddressSelector.SelectAdvertisedAddresses(
            addresses,
            IPAddress.Parse("172.20.182.22"));

        Assert.Equal([IPAddress.Parse("172.20.182.22")], selected);
    }

    [Fact]
    public void Falls_back_to_usable_gateway_addresses_when_default_route_probe_is_unavailable()
    {
        var addresses = new[]
        {
            new LanAddressCandidate(IPAddress.Parse("172.27.64.1"), IsUsable: true, HasDefaultGateway: false),
            new LanAddressCandidate(IPAddress.Parse("10.0.0.25"), IsUsable: true, HasDefaultGateway: true),
            new LanAddressCandidate(IPAddress.Parse("169.254.10.20"), IsUsable: true, HasDefaultGateway: true),
        };

        var selected = LanAddressSelector.SelectAdvertisedAddresses(addresses, defaultRouteAddress: null);

        Assert.Equal([IPAddress.Parse("10.0.0.25")], selected);
    }
}
