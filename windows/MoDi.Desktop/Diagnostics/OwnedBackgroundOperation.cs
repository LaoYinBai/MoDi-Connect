using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoDi.Desktop.Diagnostics;

/// <summary>
/// Owns one restartable background operation and makes shutdown cancel-and-join explicit.
/// </summary>
internal sealed class OwnedBackgroundOperation : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _lifetime;
    private Task? _operation;
    private bool _disposed;

    public async Task StartAsync(
        Func<CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_operation is { IsCompleted: false })
                return;

            _lifetime?.Dispose();
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                _operation = run(_lifetime.Token);
            }
            catch (Exception ex)
            {
                _operation = Task.FromException(ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lifetime = _lifetime;
            var operation = _operation;
            if (lifetime is null && operation is null)
                return;

            lifetime?.Cancel();
            if (operation is not null)
            {
                try
                {
                    await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (lifetime?.IsCancellationRequested == true)
                {
                    // Cancellation requested by this owner is the expected stop path.
                }
            }

            _operation = null;
            _lifetime = null;
            lifetime?.Dispose();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Requests cancellation without joining the operation. Process-shutdown paths use this
    /// when an external WinRT operation may ignore cancellation long enough to block exit.
    /// Normal stop/restart paths must continue to use <see cref="StopAsync"/>.
    /// </summary>
    public void RequestStop()
    {
        try
        {
            Volatile.Read(ref _lifetime)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent clean stop already owns disposal.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        _gate.Dispose();
    }
}
