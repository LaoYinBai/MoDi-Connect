using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Adapters;

public sealed class AudioSettingsAdapter : IAudioSettingsService
{
    private readonly IReceiverRuntime _runtime;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    public AudioSettingsAdapter(ReceiverController controller)
        : this(new ReceiverRuntime(controller)) { }

    internal AudioSettingsAdapter(IReceiverRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _uiContext = SynchronizationContext.Current;
        Snapshot = BuildSnapshot();
        _runtime.SnapshotChanged += OnRuntimeChanged;
    }

    public AudioSettingsSnapshot Snapshot { get; private set; }
    public event Action<AudioSettingsSnapshot>? SnapshotChanged;

    public Task<OperationResult> SetVolumeAsync(double volume, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = double.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 0;
        _runtime.Volume = normalized;
        Publish();
        return Task.FromResult(OperationResult.Success());
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _runtime.SnapshotChanged -= OnRuntimeChanged;
    }

    private void OnRuntimeChanged() => Publish();

    private void Publish()
    {
        if (_disposed)
            return;
        Snapshot = BuildSnapshot();
        var publishedSnapshot = Snapshot;
        var handler = SnapshotChanged;
        if (handler is null)
            return;
        if (_uiContext is null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
            handler(publishedSnapshot);
        else
            _uiContext.Post(_ => handler(publishedSnapshot), null);
    }

    private AudioSettingsSnapshot BuildSnapshot() => new(
        double.IsFinite(_runtime.Volume) ? Math.Clamp(_runtime.Volume, 0, 1) : 0,
        ReceiverStatusAdapter.OutputLabel(_runtime.CurrentRoute));
}
