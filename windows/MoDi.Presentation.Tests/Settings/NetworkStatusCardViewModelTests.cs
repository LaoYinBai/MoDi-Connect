using MoDi.App.Contracts;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Settings;

public sealed class NetworkStatusCardViewModelTests
{
    [Fact]
    public void Network_card_shows_current_link_address_ports_and_four_link_rows()
    {
        var network = new RecordingNetworkStatusSource();
        using var vm = new NetworkStatusCardViewModel(network);

        Assert.Equal("在家·LAN", vm.CurrentLinkLabel);
        Assert.Equal("192.168.1.100", vm.LocalIpAddress);
        Assert.Equal(12345, vm.AudioPort);
        Assert.Equal(12347, vm.HandshakePort);
        Assert.Equal([LinkKind.Lan, LinkKind.WifiDirect, LinkKind.Bluetooth, LinkKind.Usb],
            vm.Links.Select(link => link.Kind));
        Assert.Equal(LinkAvailability.Active, vm.Links[0].State);
    }

    [Fact]
    public void Dispose_unsubscribes_from_network_status()
    {
        var network = new RecordingNetworkStatusSource();
        var vm = new NetworkStatusCardViewModel(network);
        vm.Dispose();

        network.Publish(SnapshotFactory.Network() with { LocalIpAddress = "10.0.0.9" });

        Assert.Equal("192.168.1.100", vm.LocalIpAddress);
    }
}
