using Avalonia.Media.Imaging;
using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.P2p;

public sealed class QrPairingViewModel : ObservableObject, IDisposable
{
    private readonly IPairingService _pairing;
    private readonly TimeProvider _timeProvider;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly ITimer _expiryTimer;
    private Bitmap? _qrBitmap;
    private DateTimeOffset? _expiresAt;
    private bool _isRefreshing;
    private bool _isOpen;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public QrPairingViewModel(IPairingService pairing, TimeProvider timeProvider)
    {
        _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _synchronizationContext = SynchronizationContext.Current;
        _expiryTimer = timeProvider.CreateTimer(
            OnExpiryTimerTick,
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApplySnapshot(pairing.Snapshot);
        pairing.SnapshotChanged += OnSnapshotChanged;
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public Bitmap? QrBitmap
    {
        get => _qrBitmap;
        private set
        {
            if (ReferenceEquals(_qrBitmap, value))
                return;

            var previous = _qrBitmap;
            _qrBitmap = value;
            OnPropertyChanged();
            previous?.Dispose();
            NotifyQrAvailabilityChanged();
        }
    }

    public DateTimeOffset? ExpiresAt
    {
        get => _expiresAt;
        private set
        {
            if (!SetProperty(ref _expiresAt, value))
                return;

            OnPropertyChanged(nameof(ExpirationLabel));
            NotifyQrAvailabilityChanged();
        }
    }

    public string ExpirationLabel => ExpiresAt is null
        ? "等待生成"
        : IsExpired
            ? "二维码已过期"
            : $"有效至 {ExpiresAt.Value.ToLocalTime():HH:mm:ss}";

    public bool IsExpired => ExpiresAt is not null && _timeProvider.GetUtcNow() >= ExpiresAt.Value;
    public bool IsQrAvailable => QrBitmap is not null && !IsExpired;

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string? ErrorCode
    {
        get => _errorCode;
        private set => SetProperty(ref _errorCode, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value))
                return;

            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public void Open()
    {
        IsOpen = true;
        NotifyQrAvailabilityChanged();
    }

    public void Close() => IsOpen = false;
    public void Toggle() => IsOpen = !IsOpen;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pairing.SnapshotChanged -= OnSnapshotChanged;
        _expiryTimer.Dispose();
        QrBitmap = null;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        ErrorCode = null;
        ErrorMessage = null;
        try
        {
            var result = await _pairing.RefreshQrAsync(cancellationToken);
            if (!result.IsSuccess)
            {
                ErrorCode = result.ErrorCode;
                ErrorMessage = result.UserMessage;
            }
        }
        finally
        {
            IsRefreshing = _pairing.Snapshot.IsRefreshing;
        }
    }

    private void OnSnapshotChanged(PairingSnapshot snapshot) => RunOnCapturedContext(() => ApplySnapshot(snapshot));

    private void ApplySnapshot(PairingSnapshot snapshot)
    {
        if (_disposed)
            return;

        QrBitmap = QrBitmapFactory.FromPng(snapshot.QrPng);
        ExpiresAt = snapshot.ExpiresAt;
        IsRefreshing = snapshot.IsRefreshing;
        ErrorCode = snapshot.ErrorCode;
        ErrorMessage = snapshot.ErrorMessage;
        ScheduleExpiryNotification();
    }

    private void ScheduleExpiryNotification()
    {
        if (ExpiresAt is null || IsExpired)
        {
            _expiryTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        _expiryTimer.Change(ExpiresAt.Value - _timeProvider.GetUtcNow(), Timeout.InfiniteTimeSpan);
    }

    private void OnExpiryTimerTick(object? state) => RunOnCapturedContext(() =>
    {
        if (!_disposed)
            NotifyQrAvailabilityChanged();
    });

    private void NotifyQrAvailabilityChanged()
    {
        OnPropertyChanged(nameof(IsExpired));
        OnPropertyChanged(nameof(IsQrAvailable));
        OnPropertyChanged(nameof(ExpirationLabel));
    }

    private void RunOnCapturedContext(Action action)
    {
        if (_synchronizationContext is null || ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            action();
            return;
        }

        _synchronizationContext.Post(_ => action(), null);
    }
}
