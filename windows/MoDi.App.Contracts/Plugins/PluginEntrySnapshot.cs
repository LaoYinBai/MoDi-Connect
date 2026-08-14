namespace MoDi.App.Contracts;

public sealed record PluginEntrySnapshot(
    string Id,
    string DisplayName,
    bool IsBuiltIn,
    bool IsEnabled,
    bool CanUninstall,
    PluginHealth Health,
    string Detail,
    PluginDeveloperMetadata Developer);
