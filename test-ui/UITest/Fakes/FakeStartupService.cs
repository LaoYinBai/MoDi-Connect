using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeStartupService : IStartupService
{
    public StartupSnapshot Snapshot { get; private set; } = new(false, true, null, null);
    public int SetEnabledCalls { get; private set; }
    public event Action<StartupSnapshot>? SnapshotChanged;

    public Task<OperationResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        SetEnabledCalls++;
        Snapshot = Snapshot with { IsEnabled = enabled, ErrorCode = null, ErrorMessage = null };
        SnapshotChanged?.Invoke(Snapshot);
        return Task.FromResult(OperationResult.Success());
    }
}
