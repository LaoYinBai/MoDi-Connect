using MoDi.App.Contracts;

namespace MoDi.Desktop.Tests.TestDoubles;

internal sealed class RecordingExternalNavigationService : IExternalNavigationService
{
    public int Calls { get; private set; }
    public ExternalDestination? LastDestination { get; private set; }
    public OperationResult Result { get; set; } = OperationResult.Success();

    public Task<OperationResult> OpenAsync(
        ExternalDestination destination,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastDestination = destination;
        return Task.FromResult(Result);
    }
}
