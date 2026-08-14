using MoDi.Desktop.Platform.Content;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class AvaloniaClipboardServiceTests
{
    [Fact]
    public async Task Missing_host_clipboard_is_reported_without_reading_global_application_state()
    {
        var service = new AvaloniaClipboardService(() => null);

        var result = await service.CopyTextAsync("墨堤 1.0.0", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CLIPBOARD_UNAVAILABLE", result.ErrorCode);
    }
}
