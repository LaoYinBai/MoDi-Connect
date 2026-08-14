using Xunit;

namespace MoDi.Desktop.Tests.Adapters;

public sealed class AudioSettingsAdapterTests
{
    [Theory]
    [InlineData(-0.4, 0)]
    [InlineData(0.42, 0.42)]
    [InlineData(1.8, 1)]
    public async Task Volume_is_clamped_before_it_reaches_the_runtime(double requested, double expected)
    {
        var runtime = new TestReceiverRuntime();
        using var adapter = new AudioSettingsAdapter(runtime);

        var result = await adapter.SetVolumeAsync(requested, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, runtime.Volume);
        Assert.Equal(expected, adapter.Snapshot.Volume);
    }

    [Fact]
    public void Runtime_update_refreshes_the_output_device_label()
    {
        var runtime = new TestReceiverRuntime { CurrentRoute = 0 };
        using var adapter = new AudioSettingsAdapter(runtime);
        runtime.CurrentRoute = 2;

        runtime.RaiseSnapshotChanged();

        Assert.Equal("CABLE Input（虚拟麦克风）", adapter.Snapshot.OutputDeviceLabel);
    }

    [Fact]
    public void Dispose_unsubscribes_from_runtime_updates()
    {
        var runtime = new TestReceiverRuntime();
        var adapter = new AudioSettingsAdapter(runtime);

        adapter.Dispose();

        Assert.Equal(0, runtime.SnapshotSubscriberCount);
    }
}
