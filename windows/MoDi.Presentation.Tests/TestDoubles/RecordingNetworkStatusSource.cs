using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingNetworkStatusSource : INetworkStatusSource
{
    public RecordingNetworkStatusSource(NetworkStatusSnapshot? snapshot = null) =>
        Snapshot = snapshot ?? SnapshotFactory.Network();

    public NetworkStatusSnapshot Snapshot { get; private set; }
    public event Action<NetworkStatusSnapshot>? SnapshotChanged;

    public void Publish(NetworkStatusSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    public void Dispose()
    {
    }
}
