using MoDi.App.Contracts;
using MoDi.Desktop.Platform.Content;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class EmbeddedMarkdownContentProviderTests
{
    [Fact]
    public async Task Embedded_provider_uses_a_closed_resource_map()
    {
        var assembly = typeof(Program).Assembly;
        var provider = new EmbeddedMarkdownContentProvider(assembly);

        foreach (var key in Enum.GetValues<MarkdownContentKey>())
        {
            var result = await provider.GetAsync(key, CancellationToken.None);
            Assert.True(result.IsSuccess, $"{key}: {result.ErrorCode}");
            Assert.False(string.IsNullOrWhiteSpace(result.Value));
            var expectedName = EmbeddedMarkdownContentProvider.ResourceName(key);
            Assert.Single(assembly.GetManifestResourceNames(), name => name == expectedName);
        }
    }
}
