using MoDi.Presentation.Stage;

namespace MoDi.Presentation.Tests.Stage;

public sealed class InkStagePresentationTests
{
    [Fact]
    public void Forward_walk_places_foot_on_bridge_midpoint_without_extra_bounce()
    {
        var frame = StageAnimationDirector.FrameAt(
            StageConnectionState.Connected,
            TimeSpan.FromMilliseconds(600));

        var presentation = InkStagePresentation.FromFrame(frame, elapsedSeconds: 0.6, rms: 0.9);

        Assert.Equal(InkBoyMode.Walk, presentation.BoyMode);
        Assert.Equal(370d, presentation.BoyCanvasLeft, 6);
        Assert.Equal(55d, presentation.BoyCanvasTop, 6);
        Assert.False(presentation.BoyMirrored);
        Assert.Equal(0d, presentation.EffectiveRms);
    }

    [Theory]
    [InlineData(0.0, 296.0)]
    [InlineData(0.5, 142.0)]
    [InlineData(1.0, 296.0)]
    public void Walk_foot_follows_flattened_gaussian_bridge(double travel, double expectedY) =>
        Assert.Equal(expectedY, InkStageLayout.WalkFootY(travel), 6);

    [Fact]
    public void Connected_stage_uses_independent_reveals_and_enables_rms()
    {
        var connected = StageAnimationDirector.FrameAt(
            StageConnectionState.Connected,
            TimeSpan.FromMilliseconds(2680));

        var presentation = InkStagePresentation.FromFrame(connected, elapsedSeconds: 2.68, rms: 0.72);

        Assert.Equal(InkBoyMode.Stand, presentation.BoyMode);
        Assert.NotEqual(presentation.BridgeRevealOrigin, presentation.BoyRevealOrigin);
        Assert.Equal(0.82, presentation.RevealFeatherStart);
        Assert.Equal(1d, presentation.ColorReveal);
        Assert.Equal(0.72, presentation.EffectiveRms);
    }
}
