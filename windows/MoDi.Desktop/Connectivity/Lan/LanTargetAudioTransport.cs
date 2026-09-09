using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts.Connectivity;
using MoDi.Core;
using MoDi.Desktop.Connectivity.Channels;
using MoDi.Protocol;

namespace MoDi.Desktop.Connectivity.Lan;

/// <summary>
/// Dev-gated LAN data plane. The approved legacy transport still owns wire I/O;
/// accepted LAN sessions are projected through Session -> audio/primary Channel.
/// </summary>
internal sealed class LanTargetAudioTransport(ITransport legacy) : ITransport
{
    private readonly ITransport _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
    private readonly object _gate = new();
    private BoundLanTransportSession? _boundTransport;
    private LegacyAudioChannelAdapter? _channel;
    private bool _legacyFallbackSubscribed;

    internal SessionRegistry Sessions { get; } = new();
    internal ChannelRouter Channels { get; } = new();
    internal Peer? CurrentPeer { get; private set; }
    internal Session? CurrentSession { get; private set; }

    public TransportType Type => _legacy.Type;
    public bool IsConnected => _legacy.IsConnected;
    public event Action<ReadOnlyMemory<byte>>? PacketReceived;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _legacy.ConnectAsync(ct).ConfigureAwait(false);
        lock (_gate) EnableLegacyFallbackLocked();
    }

    internal void BindSession(Guid sessionUuid)
    {
        lock (_gate)
        {
            CloseCurrentLocked();
            DisableLegacyFallbackLocked();

            var id = SessionId.Parse(sessionUuid.ToString("N"));
            var peerId = PeerId.Parse($"lan-session:{sessionUuid:N}");
            CurrentPeer = new Peer(peerId, null, new HashSet<ConnectivityCapability> { ConnectivityCapability.Audio });
            CurrentSession = new Session(id, peerId, TransportKind.Lan, SessionState.Connecting);
            Sessions.ObserveStarted(id, TransportKind.Lan, SessionState.Connecting);
            ObserveStateLocked(SessionState.Authenticating);

            _boundTransport = new BoundLanTransportSession(_legacy);
            _channel = new LegacyAudioChannelAdapter(id, _boundTransport);
            Channels.Register(_channel);
            _channel.BytesReceived += OnChannelBytes;
            _channel.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
            ObserveStateLocked(SessionState.Ready);
        }
    }

    internal void UseLegacyFallback()
    {
        lock (_gate)
        {
            CloseCurrentLocked();
            EnableLegacyFallbackLocked();
        }
    }

    internal void ObserveStreaming()
    {
        lock (_gate) ObserveStateLocked(SessionState.Streaming);
    }

    internal void ObserveReconnecting()
    {
        lock (_gate) ObserveStateLocked(SessionState.Reconnecting);
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        lock (_gate)
            return _channel?.SendAsync(data, ct) ?? _legacy.SendAsync(data, ct);
    }

    public async Task DisconnectAsync()
    {
        lock (_gate)
        {
            CloseCurrentLocked();
            DisableLegacyFallbackLocked();
        }
        await _legacy.DisconnectAsync().ConfigureAwait(false);
    }

    private void ObserveStateLocked(SessionState state)
    {
        if (CurrentSession is not { } current) return;
        var result = Sessions.ObserveState(current.Id, state);
        if (result.Applied) CurrentSession = current with { State = state };
    }

    private void CloseCurrentLocked()
    {
        if (CurrentSession is not { } current) return;
        if (current.State is not SessionState.Closing and not SessionState.Closed)
            ObserveStateLocked(SessionState.Closing);
        if (_channel is not null)
        {
            _channel.BytesReceived -= OnChannelBytes;
            Channels.CloseSessionAsync(current.Id, CancellationToken.None).GetAwaiter().GetResult();
        }
        _boundTransport?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Sessions.ObserveState(current.Id, SessionState.Closed);
        CurrentSession = current with { State = SessionState.Closed };
        CurrentPeer = null;
        _channel = null;
        _boundTransport = null;
    }

    private void EnableLegacyFallbackLocked()
    {
        if (_legacyFallbackSubscribed) return;
        _legacy.PacketReceived += OnLegacyBytes;
        _legacyFallbackSubscribed = true;
    }

    private void DisableLegacyFallbackLocked()
    {
        if (!_legacyFallbackSubscribed) return;
        _legacy.PacketReceived -= OnLegacyBytes;
        _legacyFallbackSubscribed = false;
    }

    private void OnLegacyBytes(ReadOnlyMemory<byte> bytes) => PacketReceived?.Invoke(bytes);
    private void OnChannelBytes(ReadOnlyMemory<byte> bytes) => PacketReceived?.Invoke(bytes);

    private sealed class BoundLanTransportSession : ITransportSession
    {
        private readonly ITransport _legacy;
        private bool _closed;

        internal BoundLanTransportSession(ITransport legacy)
        {
            _legacy = legacy;
            _legacy.PacketReceived += OnPacketReceived;
        }

        public TransportDescriptor Descriptor => TransportDescriptor.For(TransportKind.Lan);
        public TransportSessionState State => _closed ? TransportSessionState.Closed : TransportSessionState.Connected;
        public event Action<ReadOnlyMemory<byte>>? BytesReceived;
        public Task<IReadOnlyList<TransportCandidate>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TransportCandidate>>([]);
        public Task ListenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ConnectAsync(TransportEndpoint endpoint, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
            _legacy.SendAsync(payload, cancellationToken);
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            if (!_closed)
            {
                _legacy.PacketReceived -= OnPacketReceived;
                _closed = true;
            }
            return Task.CompletedTask;
        }
        public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        private void OnPacketReceived(ReadOnlyMemory<byte> bytes) => BytesReceived?.Invoke(bytes);
    }
}

internal static class LanTargetComposition
{
    internal static bool IsEnabled(Func<string, string?>? readSetting = null)
    {
        readSetting ??= Environment.GetEnvironmentVariable;
        return string.Equals(readSetting("MODI_LAN_TARGET"), "1", StringComparison.Ordinal);
    }
}
