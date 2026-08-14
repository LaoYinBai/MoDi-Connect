namespace MoDi.Presentation.Infrastructure;

public sealed class SubscriptionBag : IDisposable
{
    private readonly List<Action> _unsubscribe = [];
    private bool _disposed;

    public void Add(Action unsubscribe)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _unsubscribe.Add(unsubscribe);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        for (var index = _unsubscribe.Count - 1; index >= 0; index--)
            _unsubscribe[index]();
        _unsubscribe.Clear();
    }
}
