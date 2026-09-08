using MoDi.App.Contracts.Connectivity;

namespace MoDi.Desktop.Platform.Logging;

public sealed record ConnectivityLogContext(
    PeerId? PeerId = null,
    SessionId? SessionId = null,
    ChannelId? ChannelId = null,
    TransportKind? Transport = null,
    ChannelDirection? Direction = null,
    long? Sequence = null,
    SessionState? State = null,
    string? OperationId = null)
{
    internal SafeConnectivityLogContext ToSafeContext() => new(
        PeerId is { } peer ? $"p:{LogRedactor.Fingerprint(peer.Value)}" : null,
        SessionId is { } session ? $"s:{LogRedactor.Fingerprint(session.Value)}" : null,
        ChannelId?.Value,
        Transport?.ToString(),
        Direction?.ToString(),
        Sequence,
        State?.ToString(),
        string.IsNullOrWhiteSpace(OperationId) ? null : $"o:{LogRedactor.Fingerprint(OperationId)}");
}

internal sealed record SafeConnectivityLogContext(
    string? Peer,
    string? Session,
    string? Channel,
    string? Transport,
    string? Direction,
    long? Sequence,
    string? State,
    string? Operation);
