namespace MoDi.App.Contracts;

public sealed record PairedDeviceSnapshot(
    string Id,
    string DisplayName,
    string LastConnectedLabel);
