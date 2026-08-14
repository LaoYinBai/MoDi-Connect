namespace MoDi.App.Contracts;

public sealed record LogExportReceipt(
    string ArchiveDisplayName,
    int IncludedFileCount);
