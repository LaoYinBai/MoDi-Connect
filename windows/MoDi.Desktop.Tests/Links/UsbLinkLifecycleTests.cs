using MoDi.Desktop.Core.Session;
using MoDi.Desktop.Links;
using Xunit;

namespace MoDi.Desktop.Tests.Links;

public sealed class UsbLinkLifecycleTests
{
    [Fact]
    public async Task Dispose_stops_the_resident_link_and_is_idempotent()
    {
        var link = new UsbLink(new ConnectionStateManager());
        Assert.True(await link.ConnectAsync());
        Assert.True(link.IsActive);

        link.Dispose();
        link.Dispose();

        Assert.False(link.IsActive);
        Assert.Equal(LinkState.Idle, link.State);
    }
}
