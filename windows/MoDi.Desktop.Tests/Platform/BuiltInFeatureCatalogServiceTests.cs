using MoDi.Desktop.Platform.Features;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class BuiltInFeatureCatalogServiceTests
{
    [Fact]
    public void Audio_is_registered_as_built_in_and_cannot_be_uninstalled()
    {
        var service = new BuiltInFeatureCatalogService([new BuiltInAudioFeature()]);

        var audio = Assert.Single(service.Snapshot.Entries);
        Assert.Equal("audio", audio.Id);
        Assert.True(audio.IsBuiltIn);
        Assert.False(audio.CanUninstall);
        Assert.False(service.Snapshot.CanImportExternal);
        Assert.Equal("Silvite", audio.Developer.Publisher);
        Assert.Contains("audio.receive", audio.Developer.DeclaredCapabilities);
    }

    [Fact]
    public async Task Built_in_uninstall_and_external_import_are_honestly_rejected()
    {
        var service = new BuiltInFeatureCatalogService([new BuiltInAudioFeature()]);

        var uninstall = await service.UninstallAsync("audio", CancellationToken.None);
        var import = await service.ImportAsync(CancellationToken.None);

        Assert.Equal("PLUGIN_BUILTIN_PROTECTED", uninstall.ErrorCode);
        Assert.Equal("PLUGIN_HOST_UNAVAILABLE", import.ErrorCode);
    }
}
