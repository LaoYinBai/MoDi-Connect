using Avalonia.Threading;
using MoDi.App.Contracts;
using MoDi.Presentation.P2p;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.P2p;

[Collection("Avalonia UI")]
public sealed class PairedDevicesViewModelTests
{
    [Fact]
    public void Snapshot_owns_only_the_device_list_and_empty_state()
    {
        var service = new RecordingPairingService(SnapshotFactory.Pairing(devices: []));
        using var vm = new PairedDevicesViewModel(service);

        Assert.Empty(vm.Devices);
        Assert.True(vm.IsEmpty);

        service.Publish(SnapshotFactory.Pairing(devices:
        [
            new PairedDeviceSnapshot("recent-p2p", "工作室 Mac", "上次连接：今天")
        ], errorCode: "QR_ONLY", errorMessage: "二维码刷新失败"));
        Dispatcher.UIThread.RunJobs();

        var device = Assert.Single(vm.Devices);
        Assert.Equal("recent-p2p", device.Id);
        Assert.Equal("工作室 Mac", device.DisplayName);
        Assert.Equal("P2P", device.LinkLabel);
        Assert.False(vm.IsEmpty);
        Assert.Null(vm.ErrorCode);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Connect_command_selects_the_device_and_delegates_once()
    {
        var service = new RecordingPairingService();
        using var vm = new PairedDevicesViewModel(service);

        await vm.ConnectCommand.ExecuteAsync("recent-p2p");

        Assert.Equal(1, service.ConnectCalls);
        Assert.Equal("recent-p2p", service.LastConnectedDeviceId);
        Assert.Equal("recent-p2p", vm.SelectedDeviceId);
        Assert.True(Assert.Single(vm.Devices).IsSelected);
    }

    [Fact]
    public async Task Reconnect_failure_stays_on_the_paired_devices_module()
    {
        var service = new RecordingPairingService
        {
            ConnectResult = OperationResult.Failure("PAIR_CONNECT", "连接失败")
        };
        using var paired = new PairedDevicesViewModel(service);
        using var qr = new QrPairingViewModel(service, TimeProvider.System);

        await paired.ConnectCommand.ExecuteAsync("recent-p2p");

        Assert.Equal("PAIR_CONNECT", paired.ErrorCode);
        Assert.Equal("连接失败", paired.ErrorMessage);
        Assert.Null(qr.ErrorCode);
        Assert.Null(qr.ErrorMessage);
    }

    [Fact]
    public void Dispose_unsubscribes_from_pairing_snapshots()
    {
        var service = new RecordingPairingService();
        var vm = new PairedDevicesViewModel(service);
        var original = Assert.Single(vm.Devices).DisplayName;

        vm.Dispose();
        service.Publish(SnapshotFactory.Pairing(devices:
        [
            new PairedDeviceSnapshot("other", "不应出现", "刚刚")
        ]));

        Assert.Equal(original, Assert.Single(vm.Devices).DisplayName);
    }
}
