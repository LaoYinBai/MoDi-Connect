using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;
using MoDi.Presentation.Markdown;

namespace MoDi.Presentation.About;

public sealed class SupportCardViewModel : ObservableObject, IDisposable
{
    private readonly IExternalNavigationService _navigation;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public SupportCardViewModel(
        IMarkdownContentProvider provider,
        MarkdownContentKey key,
        IExternalNavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        Content = new MarkdownDocumentViewModel(provider, key);
        OpenSupportCommand = new AsyncRelayCommand(OpenSupportAsync, () => !_disposed);
    }

    public string Title => "技术支持";
    public MarkdownDocumentViewModel Content { get; }
    public AsyncRelayCommand OpenSupportCommand { get; }
    public string? ErrorCode { get => _errorCode; private set => SetProperty(ref _errorCode, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Dispose()
    {
        _disposed = true;
        Content.Dispose();
        OpenSupportCommand.RaiseCanExecuteChanged();
    }

    private async Task OpenSupportAsync(CancellationToken cancellationToken)
    {
        SetError(null, null);
        try
        {
            var result = await _navigation.OpenAsync(ExternalDestination.TechnicalSupport, cancellationToken);
            if (!result.IsSuccess)
                SetError(result.ErrorCode, result.UserMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError("PRESENTATION_SUPPORT_NAVIGATION", "无法打开技术支持页面");
        }
    }

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}
