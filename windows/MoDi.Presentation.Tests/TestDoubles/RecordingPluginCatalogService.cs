using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingPluginCatalogService : IPluginCatalogService
{
    public RecordingPluginCatalogService(PluginCatalogSnapshot? snapshot = null) =>
        Snapshot = snapshot ?? SnapshotFactory.Plugins();

    public PluginCatalogSnapshot Snapshot { get; private set; }
    public OperationResult ImportResult { get; set; } = OperationResult.Success();
    public OperationResult SetEnabledResult { get; set; } = OperationResult.Success();
    public OperationResult UninstallResult { get; set; } = OperationResult.Success();
    public int ImportCalls { get; private set; }
    public int SetEnabledCalls { get; private set; }
    public int UninstallCalls { get; private set; }
    public string? LastPluginId { get; private set; }
    public bool? LastEnabled { get; private set; }
    public event Action<PluginCatalogSnapshot>? SnapshotChanged;

    public Task<OperationResult> ImportAsync(CancellationToken cancellationToken)
    {
        ImportCalls++;
        return Task.FromResult(ImportResult);
    }

    public Task<OperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        SetEnabledCalls++;
        LastPluginId = id;
        LastEnabled = enabled;
        return Task.FromResult(SetEnabledResult);
    }

    public Task<OperationResult> UninstallAsync(string id, CancellationToken cancellationToken)
    {
        UninstallCalls++;
        LastPluginId = id;
        return Task.FromResult(UninstallResult);
    }

    public void Publish(PluginCatalogSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }
}
