using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts.Connectivity;
using MoDi.Desktop.Connectivity.Channels;
using MoDi.Protocol;

namespace MoDi.Desktop.Connectivity.Legacy;

/// <summary>Shared session/channel lifecycle for a physical legacy connection authenticated by its link.</summary>
internal sealed class AuthenticatedLegacyAudioSession
{
    private readonly SessionId _id;
    private readonly ConnectedTransportSession _transport;
    private readonly LegacyAudioChannelAdapter _channel;
    private bool _bound;

    internal AuthenticatedLegacyAudioSession(
        Guid sessionUuid,
        PeerId peerId,
        TransportKind kind,
        TransportType legacyType,
        ITransport legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        _id = SessionId.Parse(sessionUuid.ToString("N"));
        Peer = new Peer(peerId, null, new HashSet<ConnectivityCapability> { ConnectivityCapability.Audio });
        Session = new Session(_id, peerId, kind, SessionState.Connecting);
        _transport = new ConnectedTransportSession(kind, legacy);
        _channel = new LegacyAudioChannelAdapter(_id, _transport);
        AudioTransport = new ChannelTransportAdapter(_channel, legacyType);
    }

    internal SessionRegistry Sessions { get; } = new();
    internal ChannelRouter Channels { get; } = new();
    internal Peer Peer { get; }
    internal Session Session { get; private set; }
    internal ITransport AudioTransport { get; }

    internal void Bind()
    {
        if (_bound) return;
        Sessions.ObserveStarted(_id, Session.Transport, SessionState.Connecting);
        Observe(SessionState.Authenticating);
        Channels.Register(_channel);
        Observe(SessionState.Ready);
        _bound = true;
    }

    internal void ObserveStreaming() => Observe(SessionState.Streaming);

    internal void Close()
    {
        if (Session.State == SessionState.Closed) return;
        if (Session.State != SessionState.Closing) Observe(SessionState.Closing);
        Channels.CloseSessionAsync(_id, CancellationToken.None).GetAwaiter().GetResult();
        _transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Observe(SessionState.Closed);
    }

    private void Observe(SessionState state)
    {
        var result = Sessions.ObserveState(_id, state);
        if (result.Applied) Session = Session with { State = state };
    }

    private sealed class ConnectedTransportSession : ITransportSession
    {
        private readonly ITransport _legacy;
        private bool _closed;

        internal ConnectedTransportSession(TransportKind kind, ITransport legacy)
        {
            Descriptor = TransportDescriptor.For(kind);
            _legacy = legacy;
            _legacy.PacketReceived += OnPacketReceived;
        }

        public TransportDescriptor Descriptor { get; }
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
            if (!_closed) { _legacy.PacketReceived -= OnPacketReceived; _closed = true; }
            return Task.CompletedTask;
        }
        public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        private void OnPacketReceived(ReadOnlyMemory<byte> payload) => BytesReceived?.Invoke(payload);
    }
}
