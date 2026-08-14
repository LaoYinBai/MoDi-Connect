namespace MoDi.App.Contracts;

public sealed record PluginDeveloperMetadata(
    string Version,
    string Publisher,
    IReadOnlyList<string> DeclaredCapabilities);
