using MoDi.App.Contracts;
using MoDi.Desktop.Platform.Appearance;

namespace MoDi.Desktop.Tests.TestDoubles;

internal sealed class RecordingAppearanceResetTarget : IAppearanceResetTarget
{
    public int ResetCalls { get; private set; }
    public AppearanceSnapshot? LastReset { get; private set; }

    public Task<OperationResult> ResetToDefaultsAsync(CancellationToken cancellationToken)
    {
        ResetCalls++;
        LastReset = AppearanceSnapshot.Default;
        return Task.FromResult(OperationResult.Success());
    }
}
