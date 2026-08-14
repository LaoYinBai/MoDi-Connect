using MoDi.App.Contracts;
using Xunit;

namespace MoDi.Desktop.Tests.Adapters;

public sealed class ReceiverStatusAdapterTests
{
    [Theory]
    [InlineData(ConnectionState.Idle, ReceiverState.Idle)]
    [InlineData(ConnectionState.Disconnected, ReceiverState.Idle)]
    [InlineData(ConnectionState.Searching, ReceiverState.Searching)]
    [InlineData(ConnectionState.Found, ReceiverState.Found)]
    [InlineData(ConnectionState.Connecting, ReceiverState.Connecting)]
    [InlineData(ConnectionState.Connected, ReceiverState.Connected)]
    [InlineData(ConnectionState.Streaming, ReceiverState.Streaming)]
    [InlineData(ConnectionState.Reconnecting, ReceiverState.Reconnecting)]
    [InlineData(ConnectionState.Error, ReceiverState.Error)]
    public void Maps_every_runtime_connection_state(ConnectionState source, ReceiverState expected)
    {
        var runtime = new TestReceiverRuntime { ConnectionState = source };

        using var adapter = new ReceiverStatusAdapter(runtime);

        Assert.Equal(expected, adapter.Snapshot.State);
    }

    [Theory]
    [InlineData("lan", LinkKind.Lan)]
    [InlineData("wifi-direct", LinkKind.WifiDirect)]
    [InlineData("bluetooth", LinkKind.Bluetooth)]
    [InlineData("usb", LinkKind.Usb)]
    [InlineData("none", LinkKind.None)]
    public void Maps_known_active_links(string source, LinkKind expected)
    {
        using var adapter = new ReceiverStatusAdapter(new TestReceiverRuntime { ActiveLink = source });

        Assert.Equal(expected, adapter.Snapshot.ActiveLink);
        Assert.Null(adapter.Snapshot.ErrorCode);
    }

    [Fact]
    public void Unknown_link_falls_back_to_none_with_module_error()
    {
        using var adapter = new ReceiverStatusAdapter(new TestReceiverRuntime { ActiveLink = "future-link" });

        Assert.Equal(LinkKind.None, adapter.Snapshot.ActiveLink);
        Assert.Equal("RECEIVER_UNKNOWN_LINK", adapter.Snapshot.ErrorCode);
    }

    [Theory]
    [InlineData(0, "手机系统音频 → 电脑扬声器", "系统默认扬声器")]
    [InlineData(1, "手机系统音频 + 麦克风 → 电脑扬声器", "系统默认扬声器")]
    [InlineData(2, "手机麦克风 → 电脑虚拟麦克风", "CABLE Input（虚拟麦克风）")]
    [InlineData(3, "手机系统音频 → 电脑虚拟麦克风", "CABLE Input（虚拟麦克风）")]
    public void Maps_route_and_output_labels(int route, string routeLabel, string outputLabel)
    {
        using var adapter = new ReceiverStatusAdapter(new TestReceiverRuntime { CurrentRoute = route });

        Assert.Equal(routeLabel, adapter.Snapshot.RouteLabel);
        Assert.Equal(outputLabel, adapter.Snapshot.OutputDeviceLabel);
    }

    [Fact]
    public void Link_rows_follow_the_fixed_availability_precedence()
    {
        var runtime = new TestReceiverRuntime
        {
            ConnectionState = ConnectionState.Connected,
            ActiveLink = "lan",
            LanStatus = "等待启动",
            P2pStatus = "就绪：等待连接",
            BluetoothStatus = "蓝牙正在启动",
            UsbStatus = "USB 链路异常",
            IsP2pProgressVisible = true,
        };

        using var adapter = new ReceiverStatusAdapter(runtime);

        Assert.Collection(adapter.Snapshot.Links,
            link => Assert.Equal(LinkAvailability.Active, link.State),
            link => Assert.Equal(LinkAvailability.Connecting, link.State),
            link => Assert.Equal(LinkAvailability.Starting, link.State),
            link => Assert.Equal(LinkAvailability.Error, link.State));
    }

    [Theory]
    [InlineData("等待启动", LinkAvailability.Inactive)]
    [InlineData("就绪：等待手机连接", LinkAvailability.Listening)]
    [InlineData("启动失败", LinkAvailability.Error)]
    [InlineData("出现错误", LinkAvailability.Error)]
    public void Maps_resident_status_text(string status, LinkAvailability expected)
    {
        var runtime = new TestReceiverRuntime { LanStatus = status, ActiveLink = "usb" };

        using var adapter = new ReceiverStatusAdapter(runtime);

        Assert.Equal(expected, adapter.Snapshot.Links[0].State);
    }

    [Fact]
    public async Task Initialize_calls_runtime_exactly_once_and_publishes()
    {
        var runtime = new TestReceiverRuntime();
        using var adapter = new ReceiverStatusAdapter(runtime);
        var published = 0;
        adapter.SnapshotChanged += _ => published++;

        var first = await adapter.InitializeAsync(CancellationToken.None);
        var second = await adapter.InitializeAsync(CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, runtime.InitializeCalls);
        Assert.Equal(1, published);
    }

    [Fact]
    public void Dispose_unsubscribes_from_runtime_updates()
    {
        var runtime = new TestReceiverRuntime();
        var adapter = new ReceiverStatusAdapter(runtime);

        Assert.Equal(1, runtime.SnapshotSubscriberCount);
        adapter.Dispose();

        Assert.Equal(0, runtime.SnapshotSubscriberCount);
    }
}
