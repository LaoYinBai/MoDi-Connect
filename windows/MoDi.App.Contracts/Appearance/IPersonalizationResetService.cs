namespace MoDi.App.Contracts;

public interface IPersonalizationResetService
{
    Task<OperationResult> ResetAsync(CancellationToken cancellationToken);
}
