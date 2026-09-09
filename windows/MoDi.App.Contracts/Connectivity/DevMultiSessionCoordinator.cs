namespace MoDi.App.Contracts.Connectivity;

/// <summary>
/// Development-only isolation harness. It is not wired into production composition and accepts
/// only independently owned transport sessions; it does not multiplex the approved wire format.
/// </summary>
public sealed class DevMultiSessionCoordinator(bool enabled = false) : IAsyncDisposable
{
    private sealed record OwnedSession(ITransportSession Transport);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<SessionId, OwnedSession> _active = [];
    private bool _disposed;

    public SessionRegistry Sessions { get; } = new();
    public ChannelRouter Channels { get; } = new();

    public IReadOnlyCollection<SessionId> ActiveSessionIds
    {
        get
        {
            _gate.Wait();
            try { return _active.Keys.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray(); }
            finally { _gate.Release(); }
        }
    }

    public async Task StartAsync(
        SessionId sessionId,
        ITransportSession transport,
        IChannelDataPlane channel,
        CancellationToken cancellationToken)
    {
        if (!enabled) throw new InvalidOperationException("The multi-session isolation lab is disabled.");
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(channel);
        if (channel.SessionId != sessionId)
            throw new ArgumentException("Channel session does not match the requested session.", nameof(channel));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active.ContainsKey(sessionId))
                throw new InvalidOperationException("Session is already active in the isolation lab.");

            Channels.Register(channel);
            try
            {
                await channel.OpenAsync(cancellationToken).ConfigureAwait(false);
                var observed = Sessions.ObserveStarted(sessionId, transport.Descriptor.Kind, SessionState.Ready);
                if (!observed.Applied) throw new InvalidOperationException(observed.Reason);
                _active.Add(sessionId, new OwnedSession(transport));
            }
            catch
            {
                await Channels.CloseSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public SessionObservationResult ObserveState(SessionId sessionId, SessionState state) =>
        Sessions.ObserveState(sessionId, state);

    public async Task CloseSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_active.Remove(sessionId, out var owned)) return;
            Sessions.ObserveState(sessionId, SessionState.Closing);
            await Channels.CloseSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            await owned.Transport.CloseAsync(cancellationToken).ConfigureAwait(false);
            await owned.Transport.DisposeAsync().ConfigureAwait(false);
            Sessions.ObserveEnded(sessionId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CloseAllAsync(CancellationToken cancellationToken)
    {
        SessionId[] ids;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { ids = _active.Keys.ToArray(); }
        finally { _gate.Release(); }

        foreach (var id in ids)
            await CloseSessionAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await CloseAllAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        _gate.Dispose();
    }
}
