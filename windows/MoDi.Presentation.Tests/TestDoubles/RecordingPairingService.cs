using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingPairingService : IPairingService
{
    public RecordingPairingService(PairingSnapshot? snapshot = null) =>
        Snapshot = snapshot ?? SnapshotFactory.Pairing();

    public PairingSnapshot Snapshot { get; private set; }
    public OperationResult RefreshResult { get; set; } = OperationResult.Success();
    public OperationResult ConnectResult { get; set; } = OperationResult.Success();
    public int RefreshCalls { get; private set; }
    public int ConnectCalls { get; private set; }
    public string? LastConnectedDeviceId { get; private set; }
    public event Action<PairingSnapshot>? SnapshotChanged;

    public Task<OperationResult> RefreshQrAsync(CancellationToken cancellationToken)
    {
        RefreshCalls++;
        return Task.FromResult(RefreshResult);
    }

    public Task<OperationResult> ConnectAsync(string deviceId, CancellationToken cancellationToken)
    {
        ConnectCalls++;
        LastConnectedDeviceId = deviceId;
        return Task.FromResult(ConnectResult);
    }

    public void Publish(PairingSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    public void Dispose()
    {
    }
}
