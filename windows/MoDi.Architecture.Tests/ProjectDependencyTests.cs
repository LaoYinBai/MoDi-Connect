namespace MoDi.Architecture.Tests;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void Shared_projects_exist_and_use_the_locked_dependency_direction()
    {
        var contracts = ProjectFile.Load("windows/MoDi.App.Contracts/MoDi.App.Contracts.csproj");
        var presentation = ProjectFile.Load("windows/MoDi.Presentation/MoDi.Presentation.csproj");
        var testUi = ProjectFile.Load("test-ui/UITest/UITest.csproj");
        var desktop = ProjectFile.Load("windows/MoDi.Desktop/MoDi.Desktop.csproj");

        Assert.Empty(contracts.PackageReferences);
        Assert.Equal(
            ["../MoDi.App.Contracts/MoDi.App.Contracts.csproj"],
            presentation.ProjectReferences.Select(reference => reference.Include));
        Assert.DoesNotContain(testUi.ProjectReferences, reference => reference.Include.Contains("MoDi.Desktop"));
        Assert.Contains(testUi.ProjectReferences, reference => reference.Include.Contains("MoDi.Presentation"));
        Assert.Contains(desktop.ProjectReferences, reference => reference.Include.Contains("MoDi.Presentation"));
        Assert.All([presentation, testUi, desktop], project =>
            Assert.Equal("12.1.0", project.PackageVersion("Avalonia")));
    }
}
