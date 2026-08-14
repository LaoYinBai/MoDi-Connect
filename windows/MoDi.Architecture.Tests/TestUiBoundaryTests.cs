namespace MoDi.Architecture.Tests;

public sealed class TestUiBoundaryTests
{
    private static readonly string[] ForbiddenPackages = ["QRCoder"];

    [Fact]
    public void TestUi_has_no_production_or_platform_dependency()
    {
        var project = ProjectFile.Load("test-ui/UITest/UITest.csproj");
        Assert.DoesNotContain(project.ProjectReferences,
            reference => reference.Include.Contains("MoDi.Desktop", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(project.PackageReferences,
            package => ForbiddenPackages.Contains(package.Id, StringComparer.OrdinalIgnoreCase));

        var sourceRoot = RepositoryLayout.Resolve("test-ui/UITest");
        var sources = string.Join('\n', Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));

        Assert.DoesNotContain("Microsoft.Win32", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Process", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO.File", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("ReceiverController", sources, StringComparison.Ordinal);
    }

    [Fact]
    public void TestUi_hosts_shared_presentation_without_local_view_copies()
    {
        var mainWindow = File.ReadAllText(RepositoryLayout.Resolve("test-ui/UITest/MainWindow.axaml"));
        Assert.Contains("presentation:AppShellView", mainWindow, StringComparison.Ordinal);
        Assert.Contains("demo:DemoControlsView", mainWindow, StringComparison.Ordinal);

        string[] obsoleteViews =
        [
            "test-ui/UITest/Controls/AppChromeControl.axaml",
            "test-ui/UITest/Controls/InkStageControl.axaml",
            "test-ui/UITest/Controls/P2pOverlayControl.axaml",
            "test-ui/UITest/Views/SettingsPage.axaml",
            "test-ui/UITest/Views/AboutPage.axaml",
        ];
        Assert.All(obsoleteViews, path => Assert.False(File.Exists(RepositoryLayout.Resolve(path)), path));
    }
}
