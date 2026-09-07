using System.Text.Json;
using MoDi.App.Contracts;
using Xunit;

namespace MoDi.App.Contracts.Tests;

public sealed class SemanticVersionTests
{
    [Fact]
    public void Shared_vectors_have_the_expected_order_and_tag_equivalence()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindVectors()));
        foreach (var pair in document.RootElement.GetProperty("orderedPairs").EnumerateArray())
            Assert.True(SemanticVersion.Parse(pair[0].GetString()!) < SemanticVersion.Parse(pair[1].GetString()!));
        foreach (var pair in document.RootElement.GetProperty("equivalentPairs").EnumerateArray())
            Assert.Equal(SemanticVersion.Parse(pair[0].GetString()!), SemanticVersion.Parse(pair[1].GetString()!));
    }

    private static string FindVectors()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", "version", "semver-test-vectors.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Shared SemVer vectors were not found.");
    }
}
