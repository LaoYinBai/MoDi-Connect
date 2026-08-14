using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingStartupService : IStartupService
{
    public RecordingStartupService(StartupSnapshot? snapshot = null) =>
        Snapshot = snapshot ?? new StartupSnapshot(false, true, null, null);

    public StartupSnapshot Snapshot { get; private set; }
    public OperationResult SetEnabledResult { get; set; } = OperationResult.Success();
    public Exception? SetEnabledException { get; set; }
    public int SetEnabledCalls { get; private set; }
    public bool? LastEnabled { get; private set; }
    public event Action<StartupSnapshot>? SnapshotChanged;

    public Task<OperationResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        SetEnabledCalls++;
        LastEnabled = enabled;
        if (SetEnabledException is not null)
            throw SetEnabledException;
        if (SetEnabledResult.IsSuccess)
            Publish(Snapshot with { IsEnabled = enabled, ErrorCode = null, ErrorMessage = null });
        return Task.FromResult(SetEnabledResult);
    }

    public void Publish(StartupSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }
}
