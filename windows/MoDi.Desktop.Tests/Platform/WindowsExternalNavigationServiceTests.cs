using MoDi.App.Contracts;
using MoDi.Desktop.Platform.Content;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class WindowsExternalNavigationServiceTests
{
    [Fact]
    public async Task Only_configured_https_destinations_are_launched()
    {
        var launcher = new RecordingProcessLauncher();
        var service = new WindowsExternalNavigationService(
            new Dictionary<ExternalDestination, Uri>
            {
                [ExternalDestination.ProjectHome] = new("https://example.com/project"),
            },
            launcher);

        var result = await service.OpenAsync(ExternalDestination.ProjectHome, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://example.com/project", launcher.LastTarget);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("file:///C:/secret")]
    [InlineData("javascript:alert(1)")]
    public async Task Non_https_schemes_are_rejected(string target)
    {
        var service = new WindowsExternalNavigationService(
            new Dictionary<ExternalDestination, Uri>
            {
                [ExternalDestination.ProjectHome] = new(target),
            },
            new RecordingProcessLauncher());

        var result = await service.OpenAsync(ExternalDestination.ProjectHome, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("NAV_SCHEME_REJECTED", result.ErrorCode);
    }
}
