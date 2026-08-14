using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingReceiverStatusSource : IReceiverStatusSource
{
    public RecordingReceiverStatusSource(ReceiverSnapshot? snapshot = null) =>
        Snapshot = snapshot ?? SnapshotFactory.Receiver();

    public ReceiverSnapshot Snapshot { get; private set; }
    public event Action<ReceiverSnapshot>? SnapshotChanged;

    public Task<OperationResult> InitializeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult.Success());

    public void Publish(ReceiverSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    public void Dispose()
    {
    }
}
