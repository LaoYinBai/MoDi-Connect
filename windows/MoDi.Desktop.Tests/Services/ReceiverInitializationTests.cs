using MoDi.Desktop.Services;
using Xunit;

namespace MoDi.Desktop.Tests.Services;

public sealed class ReceiverInitializationTests
{
    [Fact]
    public async Task Failure_is_isolated_and_only_failed_links_are_retried()
    {
        var initialization = new ReceiverInitialization();
        var lan = 0;
        var usb = 0;
        var links = new (string Name, Func<Task<bool>> Start)[] {
            ("LAN", () => Task.FromResult(++lan > 1)),
            ("USB", () => { usb++; return Task.FromResult(true); }) };
        var first = await initialization.RunAsync(links);
        Assert.Contains("LAN", first.Failed);
        Assert.Contains("部分", first.Message);
        var second = await initialization.RunAsync(links);
        Assert.Empty(second.Failed);
        Assert.Equal(2, lan);
        Assert.Equal(1, usb);
    }

    [Fact]
    public async Task Thrown_exception_does_not_prevent_other_links_starting()
    {
        var result = await new ReceiverInitialization().RunAsync(new (string, Func<Task<bool>>)[] {
            ("LAN", () => throw new IOException("mDNS unavailable")),
            ("USB", () => Task.FromResult(true)) });
        Assert.Contains("LAN", result.Failed);
        Assert.Contains("USB", result.Ready);
    }
}
