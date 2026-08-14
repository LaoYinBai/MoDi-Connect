using System.Xml.Linq;

namespace MoDi.Architecture.Tests;

internal sealed record ProjectReferenceInfo(string Include);
internal sealed record PackageReferenceInfo(string Id, string Version);

internal sealed class ProjectFile
{
    private ProjectFile(
        IReadOnlyList<ProjectReferenceInfo> projectReferences,
        IReadOnlyList<PackageReferenceInfo> packageReferences)
    {
        ProjectReferences = projectReferences;
        PackageReferences = packageReferences;
    }

    public IReadOnlyList<ProjectReferenceInfo> ProjectReferences { get; }
    public IReadOnlyList<PackageReferenceInfo> PackageReferences { get; }

    public static ProjectFile Load(string relativePath)
    {
        var path = RepositoryLayout.Resolve(relativePath);
        Assert.True(File.Exists(path), $"Missing project: {relativePath}");

        var document = XDocument.Load(path);
        var projectReferences = document.Descendants("ProjectReference")
            .Select(element => new ProjectReferenceInfo(Normalize(element.Attribute("Include")?.Value)))
            .ToArray();
        var packageReferences = document.Descendants("PackageReference")
            .Select(element => new PackageReferenceInfo(
                element.Attribute("Include")?.Value ?? string.Empty,
                element.Attribute("Version")?.Value ?? element.Element("Version")?.Value ?? string.Empty))
            .ToArray();

        return new ProjectFile(projectReferences, packageReferences);
    }

    public string PackageVersion(string packageId) =>
        Assert.Single(PackageReferences, package => package.Id == packageId).Version;

    private static string Normalize(string? path) => (path ?? string.Empty).Replace('\\', '/');
}
