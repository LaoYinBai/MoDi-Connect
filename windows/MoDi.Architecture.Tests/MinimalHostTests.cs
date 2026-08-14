namespace MoDi.Architecture.Tests;

public sealed class MinimalHostTests
{
    [Fact]
    public void Production_main_window_is_only_a_shared_shell_host()
    {
        var xaml = File.ReadAllText(RepositoryLayout.Resolve("windows/MoDi.Desktop/MainWindow.axaml"));

        Assert.Contains("presentation:AppShellView", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SettingsPage", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AboutPage", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("BridgeStage", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("P2pPanel", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_has_no_second_page_or_stage_implementation()
    {
        string[] obsoletePaths =
        [
            "windows/MoDi.Desktop/Controls/BridgeStage.axaml",
            "windows/MoDi.Desktop/Controls/P2pPanel.axaml",
            "windows/MoDi.Desktop/Controls/RecentDevicePanel.axaml",
            "windows/MoDi.Desktop/ViewModels/MainWindowViewModel.cs",
            "windows/MoDi.Desktop/Styles/Colors.axaml",
            "windows/MoDi.Desktop/Styles/Controls.axaml",
        ];

        Assert.All(obsoletePaths, path => Assert.False(File.Exists(RepositoryLayout.Resolve(path)), path));
    }

    [Fact]
    public void App_is_the_only_production_composition_entry()
    {
        var app = File.ReadAllText(RepositoryLayout.Resolve("windows/MoDi.Desktop/App.axaml.cs"));
        var window = File.ReadAllText(RepositoryLayout.Resolve("windows/MoDi.Desktop/MainWindow.axaml.cs"));

        Assert.Contains("ProductionComposition.Create", app, StringComparison.Ordinal);
        Assert.DoesNotContain("ReceiverController", app, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductionComposition", window, StringComparison.Ordinal);
        Assert.DoesNotContain("ReceiverController", window, StringComparison.Ordinal);
    }
}
