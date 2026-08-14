namespace MoDi.App.Contracts.Tests.Appearance;

public sealed class AppearanceSnapshotTests
{
    [Fact]
    public void Default_matches_the_accepted_ink_night_shell()
    {
        var snapshot = AppearanceSnapshot.Default;

        Assert.Equal(ThemePreset.InkNight, snapshot.Preset);
        Assert.Equal("#151A1D", snapshot.Palette.Background);
        Assert.Equal("#E8863C", snapshot.Palette.Accent);
        Assert.Equal(200d, snapshot.FeatureRailWidth);
        Assert.False(snapshot.ReduceMotion);
        Assert.Null(snapshot.BackgroundDisplayName);
    }

    [Fact]
    public void Appearance_snapshot_is_replaced_as_a_whole()
    {
        var updated = AppearanceSnapshot.Default with
        {
            Preset = ThemePreset.PaperDay,
            FeatureRailWidth = 56d,
        };

        Assert.Equal(ThemePreset.PaperDay, updated.Preset);
        Assert.Equal(56d, updated.FeatureRailWidth);
        Assert.Equal(200d, AppearanceSnapshot.Default.FeatureRailWidth);
    }
}
