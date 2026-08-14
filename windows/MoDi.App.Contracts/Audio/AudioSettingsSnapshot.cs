namespace MoDi.App.Contracts;

public sealed record AudioSettingsSnapshot(
    double Volume,
    string OutputDeviceLabel);
