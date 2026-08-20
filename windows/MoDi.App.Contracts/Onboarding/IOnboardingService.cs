namespace MoDi.App.Contracts;

public interface IOnboardingService : IStateSource<OnboardingSnapshot>
{
    Task<OperationResult> RunDiagnosticsAsync(CancellationToken cancellationToken);
    Task<OperationResult> CompleteAsync(CancellationToken cancellationToken);
    Task<OperationResult> SkipAsync(CancellationToken cancellationToken);
}
