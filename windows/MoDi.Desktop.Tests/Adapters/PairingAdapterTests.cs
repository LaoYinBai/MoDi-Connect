using MoDi.App.Contracts;
using Xunit;

namespace MoDi.Desktop.Tests.Adapters;

public sealed class PairingAdapterTests
{
    [Fact]
    public void Qr_event_builds_png_bytes_and_two_minute_expiry()
    {
        var runtime = new TestReceiverRuntime();
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 8, 0, 0, TimeSpan.Zero));
        using var adapter = new PairingAdapter(runtime, time, payload => [1, 2, (byte)payload.Length]);

        runtime.RaiseQrPayloadChanged("MODI://pair", "工作站");

        Assert.Equal(new byte[] { 1, 2, 11 }, adapter.Snapshot.QrPng.ToArray());
        Assert.Equal("工作站", adapter.Snapshot.DeviceName);
        Assert.Equal(time.GetUtcNow().AddMinutes(2), adapter.Snapshot.ExpiresAt);
    }

    [Fact]
    public void Exposes_at_most_the_existing_recent_pair()
    {
        var runtime = new TestReceiverRuntime
        {
            RecentPair = new PairedDeviceStore.PairedInfo
            {
                PeerDeviceName = "Pixel",
                LastConnected = new DateTime(2026, 8, 10, 21, 30, 0),
            },
        };

        using var adapter = new PairingAdapter(runtime, TimeProvider.System, _ => [1]);

        var device = Assert.Single(adapter.Snapshot.Devices);
        Assert.Equal("recent-p2p", device.Id);
        Assert.Equal("Pixel", device.DisplayName);
        Assert.Contains("2026-08-10 21:30", device.LastConnectedLabel);
    }

    [Fact]
    public async Task Refresh_and_reconnect_delegate_only_to_runtime()
    {
        var runtime = new TestReceiverRuntime
        {
            RecentPair = new PairedDeviceStore.PairedInfo { PeerDeviceName = "手机" },
        };
        using var adapter = new PairingAdapter(runtime, TimeProvider.System, _ => [1]);

        var refresh = await adapter.RefreshQrAsync(CancellationToken.None);
        var connect = await adapter.ConnectAsync("recent-p2p", CancellationToken.None);

        Assert.True(refresh.IsSuccess);
        Assert.True(connect.IsSuccess);
        Assert.Equal(1, runtime.RefreshP2pCalls);
        Assert.Equal(1, runtime.ConnectRecentP2pCalls);
    }

    [Fact]
    public async Task Unknown_device_is_rejected_without_touching_runtime()
    {
        var runtime = new TestReceiverRuntime();
        using var adapter = new PairingAdapter(runtime, TimeProvider.System, _ => [1]);

        var result = await adapter.ConnectAsync("unknown", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("PAIR_DEVICE_NOT_FOUND", result.ErrorCode);
        Assert.Equal(0, runtime.ConnectRecentP2pCalls);
    }

    [Fact]
    public void Dispose_unsubscribes_from_both_runtime_events()
    {
        var runtime = new TestReceiverRuntime();
        var adapter = new PairingAdapter(runtime, TimeProvider.System, _ => [1]);

        adapter.Dispose();

        Assert.Equal(0, runtime.SnapshotSubscriberCount);
        Assert.Equal(0, runtime.QrSubscriberCount);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
