using System.Text.RegularExpressions;

namespace MoDi.App.Contracts.Connectivity;

public readonly record struct PeerId
{
    private const int MaximumLength = 128;
    private PeerId(string value) => Value = value;
    public string Value { get; }

    public static PeerId Parse(string value) =>
        TryParse(value, out var result) ? result : throw new FormatException("PeerId must be a non-empty opaque identifier.");

    public static bool TryParse(string? value, out PeerId result)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || value != value.Trim())
        {
            result = default;
            return false;
        }
        result = new PeerId(value);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct SessionId
{
    private const int MaximumLength = 128;
    private SessionId(string value) => Value = value;
    public string Value { get; }

    public static SessionId Parse(string value) =>
        TryParse(value, out var result) ? result : throw new FormatException("SessionId must be a non-empty opaque identifier.");

    public static bool TryParse(string? value, out SessionId result)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || value != value.Trim())
        {
            result = default;
            return false;
        }
        result = new SessionId(value);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ChannelId
{
    private static readonly Regex Pattern = new("^[a-z0-9][a-z0-9._-]*(/[a-z0-9][a-z0-9._-]*)*$", RegexOptions.CultureInvariant);
    private const int MaximumLength = 128;
    private ChannelId(string value) => Value = value;
    public string Value { get; }

    public static ChannelId Parse(string value) =>
        TryParse(value, out var result) ? result : throw new FormatException("ChannelId must be a canonical lowercase path.");

    public static bool TryParse(string? value, out ChannelId result)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || !Pattern.IsMatch(value))
        {
            result = default;
            return false;
        }
        result = new ChannelId(value);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;
}

public enum TransportKind { Lan, WifiDirect, Bluetooth, Usb }
public enum ConnectivityCapability { Audio, Clipboard }
public enum ChannelKind { Audio, Clipboard }
public enum ChannelDirection { Send, Receive, Duplex }
public enum ConnectivityErrorCode { None, Unavailable, Unauthorized, Timeout, TransportFailure, ProtocolFailure, Cancelled }
public enum SessionState { Idle, Discovering, Connecting, Authenticating, Ready, Streaming, Reconnecting, Closing, Closed, Failed }

public sealed record Peer(PeerId Id, string? DisplayName, IReadOnlySet<ConnectivityCapability> Capabilities);
public sealed record Session(SessionId Id, PeerId PeerId, TransportKind Transport, SessionState State);
public sealed record Channel(ChannelId Id, ChannelKind Kind, ChannelDirection Direction, long NextSequence = 0);
public sealed record ConnectivityError(ConnectivityErrorCode Code, string Message, bool IsRetryable);

public static class SessionStateMachine
{
    private static readonly IReadOnlyDictionary<SessionState, IReadOnlySet<SessionState>> Allowed =
        new Dictionary<SessionState, IReadOnlySet<SessionState>>
        {
            [SessionState.Idle] = Set(SessionState.Discovering, SessionState.Connecting, SessionState.Closing, SessionState.Failed),
            [SessionState.Discovering] = Set(SessionState.Idle, SessionState.Connecting, SessionState.Closing, SessionState.Failed),
            [SessionState.Connecting] = Set(SessionState.Authenticating, SessionState.Reconnecting, SessionState.Closing, SessionState.Failed),
            [SessionState.Authenticating] = Set(SessionState.Ready, SessionState.Reconnecting, SessionState.Closing, SessionState.Failed),
            [SessionState.Ready] = Set(SessionState.Streaming, SessionState.Reconnecting, SessionState.Closing, SessionState.Failed),
            [SessionState.Streaming] = Set(SessionState.Ready, SessionState.Reconnecting, SessionState.Closing, SessionState.Failed),
            [SessionState.Reconnecting] = Set(SessionState.Connecting, SessionState.Authenticating, SessionState.Ready, SessionState.Closing, SessionState.Failed),
            [SessionState.Closing] = Set(SessionState.Closed, SessionState.Failed),
            [SessionState.Failed] = Set(SessionState.Reconnecting, SessionState.Closing, SessionState.Closed),
            [SessionState.Closed] = Set(),
        };

    public static bool CanTransition(SessionState from, SessionState to) =>
        from == to || Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    private static IReadOnlySet<SessionState> Set(params SessionState[] states) => states.ToHashSet();
}
