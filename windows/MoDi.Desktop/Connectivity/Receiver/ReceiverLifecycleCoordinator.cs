using System;
using System.Threading.Tasks;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Connectivity.Receiver;

internal sealed record ReceiverStartupResult(
    ReceiverInitializationResult Links,
    string? P2pError);

internal sealed class ReceiverLifecycleCoordinator(IReceiverLinkRuntime links) : IDisposable
{
    private readonly IReceiverLinkRuntime _links = links ?? throw new ArgumentNullException(nameof(links));
    private readonly ReceiverInitialization _initialization = new();
    private bool _p2pStartingOrReady;
    private bool _disposed;

    internal async Task<ReceiverStartupResult> InitializeAsync()
    {
        ThrowIfDisposed();
        var result = await _initialization.RunAsync(new (string, Func<Task<bool>>)[]
        {
            ("LAN", _links.StartLanAsync),
            ("蓝牙", _links.StartBluetoothAsync),
            ("USB", _links.StartUsbAsync),
        }).ConfigureAwait(false);
        return new(result, await EnsureP2pStartedAsync().ConfigureAwait(false));
    }

    internal async Task<string?> RestartP2pAsync()
    {
        ThrowIfDisposed();
        await _links.StopP2pAsync().ConfigureAwait(false);
        _p2pStartingOrReady = false;
        return await EnsureP2pStartedAsync().ConfigureAwait(false);
    }

    internal bool ConnectP2pCandidate(string deviceId)
    {
        ThrowIfDisposed();
        return _links.ConnectP2pCandidate(deviceId);
    }

    private async Task<string?> EnsureP2pStartedAsync()
    {
        if (_p2pStartingOrReady) return null;
        _p2pStartingOrReady = true;
        try
        {
            _p2pStartingOrReady = await _links.StartP2pAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            _p2pStartingOrReady = false;
            return $"P2P 启动失败：{ex.Message}";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _links.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
