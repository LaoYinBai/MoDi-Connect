using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Stage;

public sealed class BridgeStageViewModel : ObservableObject, IDisposable
{
    private readonly IReceiverStatusSource _receiver;
    private readonly IAppearanceService _appearance;
    private readonly SynchronizationContext? _uiContext;
    private readonly StageAnimationDirector _director;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _directorTask;
    private StageConnectionState _state;
    private StageAnimationFrame _frame;
    private double _rms;
    private bool _reduceMotion;
    private bool _disposed;

    public BridgeStageViewModel(
        IReceiverStatusSource receiver,
        IAppearanceService appearance,
        TimeProvider timeProvider)
    {
        _receiver = receiver;
        _appearance = appearance;
        _uiContext = SynchronizationContext.Current;
        _state = Map(receiver.Snapshot.State);
        _rms = NormalizeRms(receiver.Snapshot.Rms);
        _reduceMotion = appearance.Snapshot.ReduceMotion;
        _frame = StageAnimationDirector.SettledFrame(_state);
        _director = new StageAnimationDirector(new TimeProviderStageClock(timeProvider));
        _director.FramePublished += OnFramePublished;
        _receiver.SnapshotChanged += OnReceiverChanged;
        _appearance.SnapshotChanged += OnAppearanceChanged;
        _directorTask = Task.Run(() => _director.RunAsync(_cancellation.Token));
        _ = _directorTask.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    public StageConnectionState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public StageAnimationFrame Frame
    {
        get => _frame;
        private set => SetProperty(ref _frame, value);
    }

    public double Rms
    {
        get => _rms;
        private set => SetProperty(ref _rms, value);
    }

    private void OnReceiverChanged(ReceiverSnapshot snapshot)
    {
        State = Map(snapshot.State);
        Rms = NormalizeRms(snapshot.Rms);
        if (_reduceMotion)
            Frame = StageAnimationDirector.SettledFrame(State);
        else
            _director.Request(State);
    }

    private void OnAppearanceChanged(AppearanceSnapshot snapshot)
    {
        _reduceMotion = snapshot.ReduceMotion;
        if (_reduceMotion)
            Frame = StageAnimationDirector.SettledFrame(State);
    }

    private void OnFramePublished(StageAnimationFrame frame)
    {
        if (_uiContext is null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
        {
            Frame = frame;
            return;
        }

        _uiContext.Post(_ => Frame = frame, null);
    }

    private static StageConnectionState Map(ReceiverState state) => state switch
    {
        ReceiverState.Searching or ReceiverState.Found or ReceiverState.Connecting =>
            StageConnectionState.Handshaking,
        ReceiverState.Connected or ReceiverState.Streaming => StageConnectionState.Connected,
        ReceiverState.Reconnecting => StageConnectionState.Reconnecting,
        _ => StageConnectionState.Disconnected,
    };

    private static double NormalizeRms(double rms) =>
        double.IsFinite(rms) ? Math.Clamp(rms, 0, 1) : 0;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _receiver.SnapshotChanged -= OnReceiverChanged;
        _appearance.SnapshotChanged -= OnAppearanceChanged;
        _director.FramePublished -= OnFramePublished;
        _cancellation.Cancel();
        _cancellation.Dispose();
    }
}
