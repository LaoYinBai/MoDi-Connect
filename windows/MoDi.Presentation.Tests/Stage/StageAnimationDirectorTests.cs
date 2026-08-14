using MoDi.Presentation.Stage;

namespace MoDi.Presentation.Tests.Stage;

public sealed class StageAnimationDirectorTests
{
    [Theory]
    [InlineData(1199, false)]
    [InlineData(1200, true)]
    public void Forward_walk_reaches_right_bank_at_1200ms(int milliseconds, bool arrived)
    {
        var frame = StageAnimationDirector.FrameAt(
            StageConnectionState.Connected,
            TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(arrived ? 1d : 1199d / 1200d, frame.BoyTravel, 6);
        Assert.Equal(15, frame.BoyFrame);
        Assert.False(frame.BoyMirrored);
    }

    [Fact]
    public void Forward_water_and_rms_complete_at_2680ms()
    {
        var before = StageAnimationDirector.FrameAt(
            StageConnectionState.Connected,
            TimeSpan.FromMilliseconds(2679));
        var boundary = StageAnimationDirector.FrameAt(
            StageConnectionState.Connected,
            TimeSpan.FromMilliseconds(2680));

        Assert.True(before.WaterLevel < 1d);
        Assert.False(before.RmsEnabled);
        Assert.Equal(1d, boundary.WaterLevel);
        Assert.True(boundary.RmsEnabled);
    }

    [Fact]
    public void Reverse_timeline_retires_water_color_then_returns_boy()
    {
        var waterDone = StageAnimationDirector.FrameAt(
            StageConnectionState.Reconnecting,
            TimeSpan.FromMilliseconds(800));
        var colorDone = StageAnimationDirector.FrameAt(
            StageConnectionState.Reconnecting,
            TimeSpan.FromMilliseconds(1400));
        var returnDone = StageAnimationDirector.FrameAt(
            StageConnectionState.Reconnecting,
            TimeSpan.FromMilliseconds(2600));

        Assert.Equal(0d, waterDone.WaterLevel);
        Assert.Equal(1d, waterDone.ColorReveal);
        Assert.Equal(0d, colorDone.ColorReveal);
        Assert.Equal(1d, colorDone.BoyTravel);
        Assert.Equal(0d, returnDone.BoyTravel);
        Assert.True(returnDone.BoyMirrored);
    }

    [Fact]
    public async Task Queue_preserves_distinct_targets_and_drops_adjacent_duplicates()
    {
        var director = new StageAnimationDirector(new ImmediateStageClock());
        var started = new List<StageConnectionState>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        director.CeremonyStarted += target =>
        {
            started.Add(target);
            if (started.Count == 4)
                cancellation.Cancel();
        };

        director.Request(StageConnectionState.Handshaking);
        director.Request(StageConnectionState.Handshaking);
        director.Request(StageConnectionState.Connected);
        director.Request(StageConnectionState.Reconnecting);
        director.Request(StageConnectionState.Reconnecting);
        director.Request(StageConnectionState.Connected);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => director.RunAsync(cancellation.Token));

        Assert.Equal(
            [
                StageConnectionState.Handshaking,
                StageConnectionState.Connected,
                StageConnectionState.Reconnecting,
                StageConnectionState.Connected,
            ],
            started);
    }

    private sealed class ImmediateStageClock : IStageClock
    {
        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}
