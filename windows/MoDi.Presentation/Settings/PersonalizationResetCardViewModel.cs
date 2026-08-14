using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Settings;

public sealed class PersonalizationResetCardViewModel : ObservableObject, IDisposable
{
    private readonly IPersonalizationResetService _reset;
    private string? _feedbackText;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public PersonalizationResetCardViewModel(IPersonalizationResetService reset)
    {
        _reset = reset ?? throw new ArgumentNullException(nameof(reset));
        ConfirmResetCommand = new AsyncRelayCommand(ResetAsync, () => !_disposed);
    }

    public AsyncRelayCommand ConfirmResetCommand { get; }
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
        ConfirmResetCommand.RaiseCanExecuteChanged();
    }

    private async Task ResetAsync(CancellationToken cancellationToken)
    {
        SetError(null, null);
        try
        {
            var result = await _reset.ResetAsync(cancellationToken);
            if (!result.IsSuccess)
            {
                SetError(result.ErrorCode, result.UserMessage);
                return;
            }

            FeedbackText = "个性化设置已重置";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError("PRESENTATION_PERSONALIZATION_RESET", "无法重置个性化设置");
        }
    }

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}
