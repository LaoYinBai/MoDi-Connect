using MoDi.Presentation.Shell;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Shell;

public sealed class FeatureRailViewModelTests
{
    [Theory]
    [InlineData(55, 56)]
    [InlineData(100, 56)]
    [InlineData(150, 200)]
    [InlineData(240, 200)]
    public async Task Commit_snaps_and_persists_the_accepted_widths(double requested, double expected)
    {
        var appearance = new RecordingAppearanceService();
        using var viewModel = new FeatureRailViewModel(appearance, [new StubBuiltInFeature()]);

        viewModel.PreviewWidth(requested);
        await viewModel.CommitWidthCommand.ExecuteAsync();

        Assert.Equal(expected, viewModel.Width);
        Assert.Equal(expected, appearance.Snapshot.FeatureRailWidth);
    }

    [Fact]
    public void Rail_exposes_the_registered_built_in_audio_feature()
    {
        using var viewModel = new FeatureRailViewModel(
            new RecordingAppearanceService(),
            [new StubBuiltInFeature()]);

        var audio = Assert.Single(viewModel.Items);
        Assert.Equal("audio", audio.Id);
        Assert.Equal("音频", audio.DisplayName);
        Assert.True(audio.IsBuiltIn);
    }
}
