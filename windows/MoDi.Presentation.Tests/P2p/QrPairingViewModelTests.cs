using MoDi.App.Contracts;
using MoDi.Presentation.P2p;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.P2p;

[Collection("Avalonia UI")]
public sealed class QrPairingViewModelTests
{
    [Fact]
    public void Snapshot_owns_qr_expiry_and_refresh_state_without_device_data()
    {
        TestApplicationHost.Ensure();
        var now = DateTimeOffset.Parse("2026-08-11T00:00:00Z");
        var service = new RecordingPairingService(SnapshotFactory.Pairing(
            qrPng: ValidPng,
            expiresAt: now.AddMinutes(2),
            devices: [new PairedDeviceSnapshot("hidden", "不属于二维码模块", "刚刚")],
            isRefreshing: true));
        using var vm = new QrPairingViewModel(service, new FixedTimeProvider(now));

        Assert.True(vm.IsQrAvailable);
        Assert.False(vm.IsExpired);
        Assert.True(vm.IsRefreshing);
        Assert.NotNull(vm.QrBitmap);
        Assert.DoesNotContain(vm.GetType().GetProperties(), property => property.Name.Contains("Device", StringComparison.Ordinal));
    }

    [Fact]
    public void Qr_is_expired_at_the_exact_expiration_boundary()
    {
        var expiresAt = DateTimeOffset.Parse("2026-08-11T00:02:00Z");
        var service = new RecordingPairingService(SnapshotFactory.Pairing(
            qrPng: ValidPng,
            expiresAt: expiresAt));
        using var vm = new QrPairingViewModel(service, new FixedTimeProvider(expiresAt));

        Assert.True(vm.IsExpired);
        Assert.False(vm.IsQrAvailable);
    }

    [Fact]
    public async Task Refresh_command_delegates_once_and_updates_only_qr_feedback()
    {
        var service = new RecordingPairingService
        {
            RefreshResult = OperationResult.Failure("QR_REFRESH", "二维码刷新失败")
        };
        using var qr = new QrPairingViewModel(service, TimeProvider.System);
        using var paired = new PairedDevicesViewModel(service);

        await qr.RefreshCommand.ExecuteAsync();

        Assert.Equal(1, service.RefreshCalls);
        Assert.Equal("QR_REFRESH", qr.ErrorCode);
        Assert.Equal("二维码刷新失败", qr.ErrorMessage);
        Assert.Null(paired.ErrorCode);
        Assert.Null(paired.ErrorMessage);
    }

    [Fact]
    public void Snapshot_replaces_and_disposes_the_renderable_bitmap()
    {
        TestApplicationHost.Ensure();
        var service = new RecordingPairingService(SnapshotFactory.Pairing(qrPng: ValidPng));
        using var vm = new QrPairingViewModel(service, TimeProvider.System);
        var original = vm.QrBitmap;

        service.Publish(SnapshotFactory.Pairing(qrPng: ValidPng));

        Assert.NotNull(original);
        Assert.NotSame(original, vm.QrBitmap);
    }

    [Fact]
    public void Dispose_unsubscribes_from_pairing_snapshots()
    {
        var service = new RecordingPairingService(SnapshotFactory.Pairing(errorCode: "before"));
        var vm = new QrPairingViewModel(service, TimeProvider.System);

        vm.Dispose();
        service.Publish(SnapshotFactory.Pairing(errorCode: "after"));

        Assert.Equal("before", vm.ErrorCode);
    }

    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
