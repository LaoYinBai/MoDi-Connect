namespace MoDi.App.Contracts.Connectivity;

public sealed record SessionSnapshot(
    SessionId Id,
    TransportKind Transport,
    SessionState State,
    long Revision);

public readonly record struct SessionObservationResult(bool Applied, string? Reason = null);

/// <summary>
/// Observation-only registry used to compare the target session model with legacy runtime facts.
/// It deliberately exposes no connection or data-plane controls.
/// </summary>
public sealed class SessionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<SessionId, SessionSnapshot> _sessions = [];
    private long _revision;

    public IReadOnlyList<SessionSnapshot> Snapshots
    {
        get
        {
            lock (_gate)
                return _sessions.Values.OrderBy(snapshot => snapshot.Id.Value, StringComparer.Ordinal).ToArray();
        }
    }

    public SessionObservationResult ObserveStarted(SessionId id, TransportKind transport, SessionState state)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(id, out var existing))
            {
                if (existing.Transport != transport)
                    return new(false, "Session transport changed.");
                return ObserveStateLocked(existing, state);
            }

            _sessions[id] = new(id, transport, state, ++_revision);
            return new(true);
        }
    }

    public SessionObservationResult ObserveState(SessionId id, SessionState state)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(id, out var existing))
                return new(false, "Unknown session.");
            return ObserveStateLocked(existing, state);
        }
    }

    public SessionObservationResult ObserveEnded(SessionId id)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(id, out var existing))
                return new(false, "Unknown session.");
            if (existing.State == SessionState.Closed)
                return new(true);

            _sessions[id] = existing with { State = SessionState.Closed, Revision = ++_revision };
            return new(true);
        }
    }

    public void ObserveClosedAll()
    {
        lock (_gate)
        {
            foreach (var pair in _sessions.ToArray())
            {
                if (pair.Value.State != SessionState.Closed)
                    _sessions[pair.Key] = pair.Value with { State = SessionState.Closed, Revision = ++_revision };
            }
        }
    }

    private SessionObservationResult ObserveStateLocked(SessionSnapshot existing, SessionState state)
    {
        if (existing.State == state)
            return new(true);
        if (!SessionStateMachine.CanTransition(existing.State, state))
            return new(false, $"Illegal transition {existing.State} -> {state}.");

        _sessions[existing.Id] = existing with { State = state, Revision = ++_revision };
        return new(true);
    }
}
