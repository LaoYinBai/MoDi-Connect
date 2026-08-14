namespace MoDi.App.Contracts;

public sealed record StartupSnapshot(
    bool IsEnabled,
    bool IsAvailable,
    string? ErrorCode,
    string? ErrorMessage);
