namespace MoDi.App.Contracts;

public interface IExternalNavigationService
{
    Task<OperationResult> OpenAsync(
        ExternalDestination destination,
        CancellationToken cancellationToken);
}
