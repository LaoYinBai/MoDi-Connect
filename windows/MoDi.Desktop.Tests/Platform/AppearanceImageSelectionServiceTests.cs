using MoDi.Desktop.Platform.Appearance;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class AppearanceImageSelectionServiceTests
{
    [Fact]
    public async Task Missing_host_storage_provider_fails_without_global_window_lookup()
    {
        var service = new WindowsImageSelectionService(() => null);

        var result = await service.SelectImageAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("IMAGE_PICKER_UNAVAILABLE", result.ErrorCode);
    }

    [Fact]
    public async Task Picker_stream_is_bounded_to_twenty_mebibytes()
    {
        await using var stream = new MemoryStream(new byte[(20 * 1024 * 1024) + 1]);

        var bytes = await WindowsImageSelectionService.ReadBoundedAsync(stream, CancellationToken.None);

        Assert.Null(bytes);
    }
}
