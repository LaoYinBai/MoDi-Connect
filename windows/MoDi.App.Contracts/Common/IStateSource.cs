namespace MoDi.App.Contracts;

public interface IStateSource<TSnapshot> where TSnapshot : notnull
{
    TSnapshot Snapshot { get; }
    event Action<TSnapshot>? SnapshotChanged;
}
