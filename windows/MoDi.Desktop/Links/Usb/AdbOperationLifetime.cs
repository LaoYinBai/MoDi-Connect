using System;
using System.Threading;

namespace MoDi.Desktop.Links;

internal sealed class AdbOperationLifetime : IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private int _shutdownStarted;

    public bool IsShuttingDown => Volatile.Read(ref _shutdownStarted) != 0;

    public CancellationTokenSource CreateLinkedSource(CancellationToken token) =>
        CancellationTokenSource.CreateLinkedTokenSource(token, _shutdown.Token);

    public void BeginShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 0)
            _shutdown.Cancel();
    }

    public void Dispose()
    {
        BeginShutdown();
        _shutdown.Dispose();
    }
}
