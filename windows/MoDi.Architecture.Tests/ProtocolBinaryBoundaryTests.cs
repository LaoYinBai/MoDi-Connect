using System.Text.Json;
using System.Text.RegularExpressions;

namespace MoDi.Architecture.Tests;

public sealed class ProtocolBinaryBoundaryTests
{
    [Fact]
    public void Android_consumes_the_pinned_local_Maven_binary_instead_of_protocol_source()
    {
        var appBuild = File.ReadAllText(RepositoryLayout.Resolve("android/app/build.gradle.kts"));
        var settings = File.ReadAllText(RepositoryLayout.Resolve("android/settings.gradle.kts"));

        Assert.DoesNotContain("MoDi-Connect-Protocol", appBuild, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kotlin.srcDir", appBuild, StringComparison.Ordinal);
        Assert.Contains("implementation(\"com.silvite.modi:modi-protocol-jvm:0.1.1\")", appBuild, StringComparison.Ordinal);
        Assert.Contains("../third_party/modi-protocol/maven", settings, StringComparison.Ordinal);
        Assert.Contains("includeGroup(\"com.silvite.modi\")", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("modi-protocol-jvm:+", appBuild, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tasks.register<Exec>(\"verifyProtocolArtifacts\")", appBuild, StringComparison.Ordinal);
        Assert.Contains("tasks.matching { it.name == \"preBuild\" }", appBuild, StringComparison.Ordinal);
        Assert.Contains("dependsOn(verifyProtocolArtifacts)", appBuild, StringComparison.Ordinal);
        Assert.Contains("scripts/protocol/Verify-ProtocolArtifacts.ps1", appBuild, StringComparison.Ordinal);
        Assert.Contains("debugRuntimeClasspath", appBuild, StringComparison.Ordinal);
        Assert.Contains("contentEquals", appBuild, StringComparison.Ordinal);
    }

    [Fact]
    public void Windows_consumes_the_pinned_local_NuGet_binary_instead_of_a_protocol_project()
    {
        var project = ProjectFile.Load("windows/MoDi.Desktop/MoDi.Desktop.csproj");
        Assert.DoesNotContain(project.ProjectReferences, reference =>
            reference.Include.Contains("MoDi-Connect-Protocol", StringComparison.OrdinalIgnoreCase) ||
            reference.Include.Contains("MoDi.Protocol.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("0.1.1", project.PackageVersion("MoDi.Protocol"));

        var nugetConfig = File.ReadAllText(RepositoryLayout.Resolve("NuGet.config"));
        Assert.Contains("third_party/modi-protocol/nuget", nugetConfig, StringComparison.Ordinal);
        Assert.Contains("packageSourceMapping", nugetConfig, StringComparison.Ordinal);
        Assert.Contains("package pattern=\"MoDi.Protocol\"", nugetConfig, StringComparison.Ordinal);

        var projectText = File.ReadAllText(RepositoryLayout.Resolve("windows/MoDi.Desktop/MoDi.Desktop.csproj"));
        Assert.Contains("Name=\"VerifyProtocolArtifacts\"", projectText, StringComparison.Ordinal);
        Assert.Contains("scripts\\protocol\\Verify-ProtocolArtifacts.ps1", projectText, StringComparison.Ordinal);
        Assert.Contains("-RepositoryRoot", projectText, StringComparison.Ordinal);
        Assert.Contains("BeforeTargets=\"CoreCompile\"", projectText, StringComparison.Ordinal);
        Assert.Contains("-ResolvedNuGetPackageRoot", projectText, StringComparison.Ordinal);

        var verifier = File.ReadAllText(RepositoryLayout.Resolve("scripts/protocol/Verify-ProtocolArtifacts.ps1"));
        Assert.Contains("ResolvedNuGetPackageRoot", verifier, StringComparison.Ordinal);
        Assert.Contains("Resolved MoDi.Protocol DLL differs from the vendored NuGet package", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void Application_source_trees_define_no_protocol_implementation_types()
    {
        var sourceFiles = EnumerateSourceFiles("android/app/src", "*.kt")
            .Concat(EnumerateSourceFiles("windows", "*.cs"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        var definitions = sourceFiles
            .Select(path => new { Path = path, Text = File.ReadAllText(path) })
            .Where(file => Regex.IsMatch(file.Text, @"(?m)^\s*(package\s+com\.modi\.protocol|namespace\s+MoDi\.Protocol\s*;)") )
            .Select(file => Path.GetRelativePath(RepositoryLayout.Root, file.Path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(definitions);
    }

    [Fact]
    public void Legacy_protocol_implementation_directories_are_absent_from_the_application_tree()
    {
        Assert.False(Directory.Exists(RepositoryLayout.Resolve("MoDi-Connect-Protocol-zh/src")));
        Assert.False(Directory.Exists(RepositoryLayout.Resolve("MoDi-Connect-Protocol-en/src")));
    }

    [Fact]
    public void Vendored_candidate_contains_only_the_manifest_allow_list()
    {
        string[] expectedFiles =
        [
            "BINARY-REDISTRIBUTION-GRANT.txt",
            "LICENSE-PROTOCOL-BINARY.txt",
            "MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt",
            "THIRD-PARTY-NOTICES.md",
            "maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.jar",
            "maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.module",
            "maven/com/silvite/modi/modi-protocol-jvm/0.1.1/modi-protocol-jvm-0.1.1.pom",
            "nuget/MoDi.Protocol.0.1.1.nupkg",
            "protocol-artifacts.v1.json",
        ];
        var root = RepositoryLayout.Resolve("third_party/modi-protocol");
        Assert.True(Directory.Exists(root));
        var actualFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedFiles.Order(StringComparer.Ordinal), actualFiles, StringComparer.Ordinal);

        var attributes = File.ReadAllText(RepositoryLayout.Resolve(".gitattributes"));
        Assert.Contains("third_party/modi-protocol/** -text", attributes, StringComparison.Ordinal);
    }

    [Fact]
    public void Vendored_manifest_separates_proprietary_source_from_owner_approved_external_distribution()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(
            RepositoryLayout.Resolve("third_party/modi-protocol/protocol-artifacts.v1.json")));
        var root = manifest.RootElement;
        Assert.Equal("0.1.1", root.GetProperty("protocolVersion").GetString());
        Assert.Equal("PROPRIETARY_SOURCE_OWNER_ISSUED", root.GetProperty("sourceLicenseStatus").GetString());
        Assert.Equal("EXTERNAL_DISTRIBUTION_APPROVED_BY_OWNER", root.GetProperty("externalDistributionStatus").GetString());
        Assert.Matches("^[0-9a-f]{64}$", root.GetProperty("legal").GetProperty("thirdPartyNoticesSha256").GetString());
    }

    [Fact]
    public void Application_builds_declare_all_required_license_outputs_without_protocol_sources()
    {
        var androidBuild = File.ReadAllText(RepositoryLayout.Resolve("android/app/build.gradle.kts"));
        Assert.DoesNotContain("excludes += setOf(", androidBuild, StringComparison.Ordinal);
        Assert.Contains("prepareThirdPartyLegalResources", androidBuild, StringComparison.Ordinal);
        Assert.Contains("CONCENTUS-1.0.1-BSD-3-CLAUSE.txt", androidBuild, StringComparison.Ordinal);
        Assert.Contains("dependsOn(prepareThirdPartyLegalResources)", androidBuild, StringComparison.Ordinal);

        var windowsProject = File.ReadAllText(RepositoryLayout.Resolve("windows/MoDi.Desktop/MoDi.Desktop.csproj"));
        foreach (var source in new[]
        {
            "third_party\\modi-protocol\\LICENSE-PROTOCOL-BINARY.txt",
            "third_party\\modi-protocol\\BINARY-REDISTRIBUTION-GRANT.txt",
            "third_party\\modi-protocol\\MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt",
            "third_party\\modi-protocol\\THIRD-PARTY-NOTICES.md",
            "LICENSES\\Apache-2.0.txt",
            "LICENSES\\BSD-3-Clause-Concentus.txt",
        })
        {
            Assert.Contains(source, windowsProject, StringComparison.Ordinal);
        }
        Assert.Contains("Link=\"Licenses\\MoDi.Protocol\\", windowsProject, StringComparison.Ordinal);
        Assert.Contains("Link=\"Licenses\\ThirdParty\\", windowsProject, StringComparison.Ordinal);
        Assert.DoesNotContain("third_party\\modi-protocol\\**", windowsProject, StringComparison.Ordinal);
        Assert.DoesNotContain("third_party\\modi-protocol\\maven", windowsProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("third_party\\modi-protocol\\nuget", windowsProject, StringComparison.OrdinalIgnoreCase);

        var packageVerifierPath = RepositoryLayout.Resolve("scripts/license/Verify-ApplicationPackageLicenses.ps1");
        Assert.True(File.Exists(packageVerifierPath), $"Missing application package license verifier: {packageVerifierPath}");
        var packageVerifier = File.ReadAllText(packageVerifierPath);
        Assert.Contains("PROPRIETARY-PROTOCOL-LICENSE-1.0.txt", packageVerifier, StringComparison.Ordinal);
        Assert.Contains("CONCENTUS-1.0.1-BSD-3-CLAUSE.txt", packageVerifier, StringComparison.Ordinal);
        Assert.Contains("MoDi.Protocol.dll", packageVerifier, StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string relativeDirectory, string pattern)
    {
        var directory = RepositoryLayout.Resolve(relativeDirectory);
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            : [];
    }
}
