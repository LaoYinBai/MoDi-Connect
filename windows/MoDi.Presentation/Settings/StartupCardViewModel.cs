using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Settings;

public sealed class StartupCardViewModel : ObservableObject, IDisposable
{
    private readonly IStartupService _startup;
    private bool _isEnabled;
    private bool _isAvailable;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public StartupCardViewModel(IStartupService startup)
    {
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));
        ToggleCommand = new AsyncRelayCommand(ToggleAsync, () => IsAvailable && !_disposed);
        ApplySnapshot(startup.Snapshot);
        startup.SnapshotChanged += OnSnapshotChanged;
    }

    public AsyncRelayCommand ToggleCommand { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        private set => SetProperty(ref _isEnabled, value);
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        private set
        {
            if (SetProperty(ref _isAvailable, value))
                ToggleCommand.RaiseCanExecuteChanged();
        }
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
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _startup.SnapshotChanged -= OnSnapshotChanged;
        ToggleCommand.RaiseCanExecuteChanged();
    }

    private async Task ToggleAsync(CancellationToken cancellationToken)
    {
        ClearError();
        try
        {
            var result = await _startup.SetEnabledAsync(!IsEnabled, cancellationToken);
            if (!result.IsSuccess)
                SetError(result.ErrorCode, result.UserMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError("PRESENTATION_STARTUP", "无法更改开机自启，请稍后重试");
        }
    }

    private void OnSnapshotChanged(StartupSnapshot snapshot) => ApplySnapshot(snapshot);

    private void ApplySnapshot(StartupSnapshot snapshot)
    {
        if (_disposed)
            return;

        IsEnabled = snapshot.IsEnabled;
        IsAvailable = snapshot.IsAvailable;
        SetError(snapshot.ErrorCode, snapshot.ErrorMessage);
    }

    private void ClearError() => SetError(null, null);

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}
