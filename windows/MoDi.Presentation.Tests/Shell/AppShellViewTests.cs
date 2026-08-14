using Avalonia.Controls;
using Avalonia.LogicalTree;
using MoDi.Presentation.About;
using MoDi.Presentation.P2p;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Shell;
using MoDi.Presentation.Stage;

namespace MoDi.Presentation.Tests.Shell;

[Collection("Avalonia UI")]
public sealed class AppShellViewTests
{
    [Fact]
    public void Shared_shell_contains_only_focused_shared_regions()
    {
        TestApplicationHost.Ensure();
        var view = new AppShellView();

        Assert.Single(view.GetLogicalDescendants().OfType<TopBarView>());
        Assert.Single(view.GetLogicalDescendants().OfType<FeatureRailView>());
        Assert.Single(view.GetLogicalDescendants().OfType<StatusBarView>());
        Assert.DoesNotContain(view.GetLogicalDescendants(), control =>
            control.GetType().Namespace?.StartsWith("UITest", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Main_page_template_overlays_stage_and_two_independent_pairing_views()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "MoDi.Presentation", "Shell", "AppShellView.axaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains(nameof(BridgeStageView), xaml, StringComparison.Ordinal);
        Assert.Contains(nameof(PairedDevicesOverlay), xaml, StringComparison.Ordinal);
        Assert.Contains(nameof(QrPairingOverlay), xaml, StringComparison.Ordinal);
        Assert.Contains(nameof(SettingsPage), xaml, StringComparison.Ordinal);
        Assert.Contains(nameof(AboutPage), xaml, StringComparison.Ordinal);
    }
}
