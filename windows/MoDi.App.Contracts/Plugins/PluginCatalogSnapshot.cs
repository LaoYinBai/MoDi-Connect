namespace MoDi.App.Contracts;

public sealed record PluginCatalogSnapshot(
    IReadOnlyList<PluginEntrySnapshot> Entries,
    bool CanImportExternal,
    string CapabilityMessage);
