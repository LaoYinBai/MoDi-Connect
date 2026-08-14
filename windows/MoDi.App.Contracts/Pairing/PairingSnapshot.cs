namespace MoDi.App.Contracts;

public sealed record PairingSnapshot(
    ReadOnlyMemory<byte> QrPng,
    string DeviceName,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<PairedDeviceSnapshot> Devices,
    bool IsRefreshing,
    string? ErrorCode,
    string? ErrorMessage);
