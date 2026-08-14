using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;
using MoDi.Presentation.Markdown;

namespace MoDi.Presentation.About;

public sealed class SponsorCardViewModel : ObservableObject, IDisposable
{
    private readonly IExternalNavigationService _navigation;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public SponsorCardViewModel(
        IMarkdownContentProvider provider,
        MarkdownContentKey key,
        IExternalNavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        Content = new MarkdownDocumentViewModel(provider, key);
        OpenSponsorCommand = new AsyncRelayCommand(OpenSponsorAsync, () => !_disposed);
    }

    public string Title => "赞助列表";
    public MarkdownDocumentViewModel Content { get; }
    public AsyncRelayCommand OpenSponsorCommand { get; }
    public string? ErrorCode { get => _errorCode; private set => SetProperty(ref _errorCode, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Dispose()
    {
        _disposed = true;
        Content.Dispose();
        OpenSponsorCommand.RaiseCanExecuteChanged();
    }

    private async Task OpenSponsorAsync(CancellationToken cancellationToken)
    {
        SetError(null, null);
        try
        {
            var result = await _navigation.OpenAsync(ExternalDestination.SponsorPage, cancellationToken);
            if (!result.IsSuccess)
                SetError(result.ErrorCode, result.UserMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError("PRESENTATION_SPONSOR_NAVIGATION", "无法打开赞助页面");
        }
    }

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}
