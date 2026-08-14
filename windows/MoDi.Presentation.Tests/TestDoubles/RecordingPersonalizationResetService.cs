using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingPersonalizationResetService : IPersonalizationResetService
{
    public OperationResult Result { get; set; } = OperationResult.Success();
    public int ResetCalls { get; private set; }

    public Task<OperationResult> ResetAsync(CancellationToken cancellationToken)
    {
        ResetCalls++;
        return Task.FromResult(Result);
    }
}
