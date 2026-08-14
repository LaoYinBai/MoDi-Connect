using MoDi.App.Contracts;
using Xunit;

namespace MoDi.Desktop.Tests.Adapters;

public sealed class NetworkStatusAdapterTests
{
    [Fact]
    public void Publishes_controller_statuses_fixed_ports_and_injected_ipv4()
    {
        var runtime = new TestReceiverRuntime
        {
            ActiveLink = "wifi-direct",
            LanStatus = "LAN 监听中",
            P2pStatus = "P2P 监听中",
            BluetoothStatus = "蓝牙监听中",
            UsbStatus = "USB 监听中",
        };
        using var adapter = new NetworkStatusAdapter(runtime, new StubAddressResolver("10.0.0.7"));

        Assert.Equal("万能 · WiFi Direct", adapter.Snapshot.CurrentLinkLabel);
        Assert.Equal("10.0.0.7", adapter.Snapshot.LocalIpAddress);
        Assert.Equal(12345, adapter.Snapshot.AudioPort);
        Assert.Equal(12347, adapter.Snapshot.HandshakePort);
        Assert.Collection(adapter.Snapshot.Links,
            link => Assert.Equal("LAN 监听中", link.Detail),
            link => Assert.Equal("P2P 监听中", link.Detail),
            link => Assert.Equal("蓝牙监听中", link.Detail),
            link => Assert.Equal("USB 监听中", link.Detail));
    }

    [Fact]
    public void Resolver_failure_is_local_to_the_network_card()
    {
        using var adapter = new NetworkStatusAdapter(
            new TestReceiverRuntime(),
            new ThrowingAddressResolver());

        Assert.Equal("不可用", adapter.Snapshot.LocalIpAddress);
        Assert.Equal(4, adapter.Snapshot.Links.Count);
    }

    [Fact]
    public void No_active_session_is_not_reported_as_lan()
    {
        using var adapter = new NetworkStatusAdapter(
            new TestReceiverRuntime { ActiveLink = "none" },
            new StubAddressResolver("10.0.0.7"));

        Assert.Equal("当前无活跃链路", adapter.Snapshot.CurrentLinkLabel);
    }

    [Fact]
    public void Runtime_change_publishes_and_dispose_unsubscribes()
    {
        var runtime = new TestReceiverRuntime();
        var adapter = new NetworkStatusAdapter(runtime, new StubAddressResolver("127.0.0.1"));
        var published = 0;
        adapter.SnapshotChanged += _ => published++;
        runtime.ActiveLink = "usb";

        runtime.RaiseSnapshotChanged();
        adapter.Dispose();

        Assert.Equal(1, published);
        Assert.Equal("USB · ADB", adapter.Snapshot.CurrentLinkLabel);
        Assert.Equal(0, runtime.SnapshotSubscriberCount);
    }

    private sealed class StubAddressResolver(string? address) : ILocalAddressResolver
    {
        public string? GetPreferredIpv4() => address;
    }

    private sealed class ThrowingAddressResolver : ILocalAddressResolver
    {
        public string? GetPreferredIpv4() => throw new InvalidOperationException("network unavailable");
    }
}
