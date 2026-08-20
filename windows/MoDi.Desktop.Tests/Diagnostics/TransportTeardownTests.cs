using MoDi.Desktop.Diagnostics;
using Xunit;

namespace MoDi.Desktop.Tests.Diagnostics;

public sealed class TransportTeardownTests
{
    [Theory]
    [InlineData("BT_READ_LOOP_STOPPED")]
    [InlineData("USB_READ_LOOP_STOPPED")]
    [InlineData("BT_LISTEN_LOOP_STOPPED")]
    [InlineData("USB_LISTEN_LOOP_STOPPED")]
    public async Task Real_teardown_fault_is_preserved_and_does_not_escape(
        string eventCode)
    {
        var events = new List<string>();

        await TeardownObserver.AwaitAsync(
            Task.FromException(new IOException("link lost")),
            CancellationToken.None,
            eventCode,
            (code, exception) => events.Add($"{code}:{exception.Message}"));

        Assert.Equal([$"{eventCode}:link lost"], events);
    }

    [Fact]
    public async Task Owned_cancellation_is_quiet()
    {
        using var owner = new CancellationTokenSource();
        owner.Cancel();
        var events = new List<string>();

        await TeardownObserver.AwaitAsync(
            Task.FromCanceled(owner.Token),
            owner.Token,
            "BT_READ_LOOP_STOPPED",
            (code, exception) => events.Add(code));

        Assert.Empty(events);
    }
}
