namespace MoDi.App.Contracts;

public interface IStartupService : IStateSource<StartupSnapshot>
{
    Task<OperationResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken);
}
