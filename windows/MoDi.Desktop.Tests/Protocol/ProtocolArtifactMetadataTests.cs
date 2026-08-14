using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MoDi.Protocol;
using Xunit;

namespace MoDi.Desktop.Tests.Protocol;

public sealed class ProtocolArtifactMetadataTests
{
    [Fact]
    public void Desktop_loads_the_frozen_protocol_binary_from_the_vendored_NuGet_candidate()
    {
        var repositoryRoot = FindRepositoryRoot();
        var manifestPath = Path.Combine(
            repositoryRoot,
            "third_party",
            "modi-protocol",
            "protocol-artifacts.v1.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        var expectedVersion = root.GetProperty("protocolVersion").GetString();
        var expectedCommit = root.GetProperty("sourceCommit").GetString();
        var expectedVectorSha256 = root
            .GetProperty("vectorSet")
            .GetProperty("sha256")
            .GetString();

        var assembly = typeof(PacketHeaderCodec).Assembly;
        Assert.Equal("MoDi.Protocol", assembly.GetName().Name);
        Assert.Equal(new Version($"{expectedVersion}.0"), assembly.GetName().Version);
        Assert.Equal(
            expectedVersion,
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        var metadata = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
        Assert.Equal(expectedCommit, metadata["MoDiProtocolCommit"]);
        Assert.Equal(expectedVectorSha256, metadata["MoDiProtocolVectorSha256"]);

        var packagePath = Path.Combine(
            repositoryRoot,
            "third_party",
            "modi-protocol",
            "nuget",
            $"MoDi.Protocol.{expectedVersion}.nupkg");
        using var package = ZipFile.OpenRead(packagePath);
        var packagedAssembly = package.GetEntry("lib/net10.0/MoDi.Protocol.dll");
        Assert.NotNull(packagedAssembly);

        using var packagedStream = packagedAssembly.Open();
        var packagedHash = SHA256.HashData(packagedStream);
        var loadedHash = SHA256.HashData(File.ReadAllBytes(assembly.Location));
        Assert.Equal(packagedHash, loadedHash);

        var assetsPath = Path.Combine(
            repositoryRoot,
            "windows",
            "MoDi.Desktop",
            "obj",
            "project.assets.json");
        Assert.Contains(
            $"\"MoDi.Protocol/{expectedVersion}\"",
            File.ReadAllText(assetsPath),
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitMarker = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitMarker) || File.Exists(gitMarker))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test output directory.");
    }
}
