using System;
using System.Linq;
using MoDi.App.Contracts.Connectivity;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop.Connectivity.Sessions;

internal sealed class LegacySessionObserver
{
    private readonly SessionRegistry _registry;
    private SessionId? _current;

    internal LegacySessionObserver(SessionRegistry registry) => _registry = registry;

    internal SessionRegistry Registry => _registry;

    internal SessionObservationResult ObserveStarted(Guid id, TransportKind transport, ConnectionState state)
    {
        var sessionId = SessionId.Parse(id.ToString("N"));
        _current = sessionId;
        return Report(_registry.ObserveStarted(sessionId, transport, Map(state)));
    }

    internal SessionObservationResult ObserveState(ConnectionState state)
    {
        if (_current is not { } id)
            return Report(new(false, "No active legacy session."));

        if (state == ConnectionState.Connected &&
            _registry.Snapshots.FirstOrDefault(snapshot => snapshot.Id == id)?.State == SessionState.Connecting)
        {
            Report(_registry.ObserveState(id, SessionState.Authenticating));
        }
        return Report(_registry.ObserveState(id, Map(state)));
    }

    internal SessionObservationResult ObserveEnded(Guid id)
    {
        var sessionId = SessionId.Parse(id.ToString("N"));
        var result = _registry.ObserveEnded(sessionId);
        if (_current == sessionId) _current = null;
        return Report(result);
    }

    internal void ObserveClosedAll()
    {
        _registry.ObserveClosedAll();
        _current = null;
    }

    private static SessionState Map(ConnectionState state) => state switch
    {
        ConnectionState.Idle or ConnectionState.Disconnected => SessionState.Closed,
        ConnectionState.Searching or ConnectionState.Found => SessionState.Discovering,
        ConnectionState.Connecting => SessionState.Connecting,
        ConnectionState.Connected => SessionState.Ready,
        ConnectionState.Streaming => SessionState.Streaming,
        ConnectionState.Reconnecting => SessionState.Reconnecting,
        ConnectionState.Error => SessionState.Failed,
        _ => SessionState.Failed,
    };

    private static SessionObservationResult Report(SessionObservationResult result)
    {
        if (!result.Applied)
            Log.W("SessionShadow", $"Legacy observation not applied: {result.Reason}");
        return result;
    }
}

internal static class SessionShadowComposition
{
    internal static LegacySessionObserver? CreateIfEnabled(Func<string, string?>? readSetting = null)
    {
        readSetting ??= Environment.GetEnvironmentVariable;
        return string.Equals(readSetting("MODI_SESSION_SHADOW"), "1", StringComparison.Ordinal)
            ? new LegacySessionObserver(new SessionRegistry())
            : null;
    }
}
