namespace MoDi.App.Contracts;

public sealed record ReceiverSnapshot(
    ReceiverState State,
    string StatusMessage,
    LinkKind ActiveLink,
    int Route,
    string RouteLabel,
    string OutputDeviceLabel,
    double Rms,
    IReadOnlyList<LinkStatusSnapshot> Links,
    bool IsP2pProgressVisible,
    bool IsP2pProgressIndeterminate,
    double P2pProgress,
    string? ErrorCode,
    string? ErrorMessage);
