namespace MoDi.App.Contracts;

public interface IPluginCatalogService : IStateSource<PluginCatalogSnapshot>
{
    Task<OperationResult> ImportAsync(CancellationToken cancellationToken);
    Task<OperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken);
    Task<OperationResult> UninstallAsync(string id, CancellationToken cancellationToken);
}
