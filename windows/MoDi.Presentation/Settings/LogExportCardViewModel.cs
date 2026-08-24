using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Settings;

public sealed class LogExportCardViewModel : ObservableObject, IDisposable
{
    private readonly ILogExportService _logs;
    private string? _archiveDisplayName;
    private string? _feedbackText;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public LogExportCardViewModel(ILogExportService logs)
    {
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !_disposed);
    }

    public AsyncRelayCommand ExportCommand { get; }
    public string? ArchiveDisplayName { get => _archiveDisplayName; private set => SetProperty(ref _archiveDisplayName, value); }
    public string? FeedbackText { get => _feedbackText; private set => SetProperty(ref _feedbackText, value); }
    public string? ErrorCode { get => _errorCode; private set => SetProperty(ref _errorCode, value); }
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
        _disposed = true;
        ExportCommand.RaiseCanExecuteChanged();
    }

    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        SetError(null, null);
        try
        {
            var result = await _logs.ExportAsync(cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                if (result.ErrorCode == "LOG_EXPORT_CANCELLED")
                {
                    FeedbackText = "已取消导出";
                    return;
                }
                SetError(result.ErrorCode, result.UserMessage);
                return;
            }

            ArchiveDisplayName = Path.GetFileName(result.Value.ArchiveDisplayName);
            FeedbackText = $"已导出：{ArchiveDisplayName}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError("PRESENTATION_LOG_EXPORT", "无法导出日志");
        }
    }

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}
