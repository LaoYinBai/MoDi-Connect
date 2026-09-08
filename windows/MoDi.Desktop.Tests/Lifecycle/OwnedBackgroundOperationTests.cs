using MoDi.Desktop.Diagnostics;
using Xunit;

namespace MoDi.Desktop.Tests.Lifecycle;

public sealed class OwnedBackgroundOperationTests
{
    [Fact]
    public async Task Stop_cancels_joins_and_allows_a_clean_restart()
    {
        await using var owner = new OwnedBackgroundOperation();
        var entered = 0;
        var exited = 0;
        async Task Run(CancellationToken token)
        {
            Interlocked.Increment(ref entered);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { Interlocked.Increment(ref exited); }
        }

        await owner.StartAsync(Run, CancellationToken.None);
        await owner.StartAsync(Run, CancellationToken.None);
        Assert.Equal(1, entered);

        await owner.StopAsync(CancellationToken.None);
        await owner.StopAsync(CancellationToken.None);
        Assert.Equal(1, exited);

        await owner.StartAsync(Run, CancellationToken.None);
        Assert.Equal(2, entered);
        await owner.StopAsync(CancellationToken.None);
        Assert.Equal(2, exited);
    }

    [Fact]
    public async Task Dispose_is_idempotent_and_rejects_a_later_start()
    {
        var owner = new OwnedBackgroundOperation();
        await owner.DisposeAsync();
        await owner.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            owner.StartAsync(_ => Task.CompletedTask, CancellationToken.None));
    }
}
