using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingExternalNavigationService : IExternalNavigationService
{
    public OperationResult Result { get; set; } = OperationResult.Success();
    public int OpenCalls { get; private set; }
    public ExternalDestination? LastDestination { get; private set; }

    public Task<OperationResult> OpenAsync(ExternalDestination destination, CancellationToken cancellationToken)
    {
        OpenCalls++;
        LastDestination = destination;
        return Task.FromResult(Result);
    }
}
