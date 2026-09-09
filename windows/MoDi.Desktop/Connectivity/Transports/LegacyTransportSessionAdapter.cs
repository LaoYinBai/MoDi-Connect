using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts.Connectivity;
using MoDi.Core;
using MoDi.Protocol;

namespace MoDi.Desktop.Connectivity.Transports;

/// <summary>Temporary bridge from the approved legacy transport binary API to the new app-level port.</summary>
internal sealed class LegacyTransportSessionAdapter : ITransportSession
{
    private readonly ITransport _legacy;
    private readonly Func<CancellationToken, Task<IReadOnlyList<TransportCandidate>>>? _discover;
    private readonly Func<CancellationToken, Task>? _listen;
    private readonly Func<TransportEndpoint, CancellationToken, Task>? _connect;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public LegacyTransportSessionAdapter(
        TransportDescriptor descriptor,
        ITransport legacy,
        Func<CancellationToken, Task<IReadOnlyList<TransportCandidate>>>? discover = null,
        Func<CancellationToken, Task>? listen = null,
        Func<TransportEndpoint, CancellationToken, Task>? connect = null)
    {
        Descriptor = descriptor;
        _legacy = legacy;
        _discover = discover;
        _listen = listen;
        _connect = connect;
        _legacy.PacketReceived += OnLegacyPacketReceived;
    }

    public TransportDescriptor Descriptor { get; }
    public TransportSessionState State { get; private set; } = TransportSessionState.Idle;
    public event Action<ReadOnlyMemory<byte>>? BytesReceived;

    public async Task<IReadOnlyList<TransportCandidate>> DiscoverAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (!Descriptor.SupportsDiscovery) return [];
        if (_discover is null) throw new InvalidOperationException($"Discovery is not bound for {Descriptor.Kind}.");
        State = TransportSessionState.Discovering;
        try { return await _discover(cancellationToken).ConfigureAwait(false); }
        finally { if (State == TransportSessionState.Discovering) State = TransportSessionState.Idle; }
    }

    public async Task ListenAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (!Descriptor.SupportsListening || _listen is null)
            throw new InvalidOperationException($"Listening is not bound for {Descriptor.Kind}.");
        State = TransportSessionState.Listening;
        await _listen(cancellationToken).ConfigureAwait(false);
    }

    public async Task ConnectAsync(TransportEndpoint endpoint, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (State == TransportSessionState.Connected) return;
            State = TransportSessionState.Connecting;
            try
            {
                if (_connect is not null) await _connect(endpoint, cancellationToken).ConfigureAwait(false);
                else await _legacy.ConnectAsync(cancellationToken).ConfigureAwait(false);
                State = TransportSessionState.Connected;
            }
            catch
            {
                State = TransportSessionState.Failed;
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (State != TransportSessionState.Connected)
            throw new InvalidOperationException("Transport session is not connected.");
        return _legacy.SendAsync(payload, cancellationToken);
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is TransportSessionState.Closed or TransportSessionState.Idle) { State = TransportSessionState.Closed; return; }
            State = TransportSessionState.Closing;
            await _legacy.DisconnectAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            State = TransportSessionState.Closed;
        }
        catch
        {
            State = TransportSessionState.Failed;
            throw;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        _legacy.PacketReceived -= OnLegacyPacketReceived;
        if (_legacy is IDisposable disposable) disposable.Dispose();
        _disposed = true;
        _gate.Dispose();
    }

    private void OnLegacyPacketReceived(ReadOnlyMemory<byte> payload) => BytesReceived?.Invoke(payload);
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
