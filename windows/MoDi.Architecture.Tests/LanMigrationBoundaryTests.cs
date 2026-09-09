namespace MoDi.Architecture.Tests;

public sealed class LanMigrationBoundaryTests
{
    [Fact]
    public void LAN_target_path_is_gated_and_p2p_explicitly_retains_legacy_audio()
    {
        var lan = File.ReadAllText(RepositoryLayout.Resolve(
            "windows/MoDi.Desktop/Links/WifiLan/WifiLanLink.cs"));
        var manager = File.ReadAllText(RepositoryLayout.Resolve(
            "windows/MoDi.Desktop/Links/LinkManager.cs"));

        Assert.Contains("LanTargetComposition.IsEnabled", lan, StringComparison.Ordinal);
        Assert.Contains("_lanTargetAudio?.BindSession", lan, StringComparison.Ordinal);
        Assert.Contains("UseLegacyAudioPath", manager, StringComparison.Ordinal);
        Assert.Contains("LinkType.WifiDirect", manager, StringComparison.Ordinal);
    }
}
