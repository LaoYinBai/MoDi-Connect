using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;
using MoDi.Presentation.Markdown;

namespace MoDi.Presentation.About;

public sealed class AboutPageViewModel : ObservableObject, IDisposable
{
    private readonly IExternalNavigationService _navigation;
    private readonly IClipboardService _clipboard;
    private readonly ILogExportService _logs;
    private MarkdownDocumentViewModel? _activeDocument;
    private bool _isDocumentDialogOpen;
    private string? _feedbackText;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public AboutPageViewModel(
        StoryCardViewModel story,
        SupportCardViewModel support,
        SponsorCardViewModel sponsor,
        MarkdownDocumentViewModel releaseNotes,
        MarkdownDocumentViewModel thirdPartyNotices,
        IExternalNavigationService navigation,
        IClipboardService clipboard,
        ILogExportService logs,
        string version)
    {
        Story = story ?? throw new ArgumentNullException(nameof(story));
        Support = support ?? throw new ArgumentNullException(nameof(support));
        Sponsor = sponsor ?? throw new ArgumentNullException(nameof(sponsor));
        ReleaseNotes = releaseNotes ?? throw new ArgumentNullException(nameof(releaseNotes));
        ThirdPartyNotices = thirdPartyNotices ?? throw new ArgumentNullException(nameof(thirdPartyNotices));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        Version = string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;

        ContactCommand = new AsyncRelayCommand(ContactAsync, () => !_disposed);
        ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync, () => !_disposed);
        CopyInfoCommand = new AsyncRelayCommand(CopyInfoAsync, () => !_disposed);
        ShowReleaseNotesCommand = new RelayCommand(() => ShowDocument(ReleaseNotes));
        ShowThirdPartyNoticesCommand = new RelayCommand(() => ShowDocument(ThirdPartyNotices));
        CloseDocumentCommand = new RelayCommand(CloseDocument);
    }

    public string DisplayName => "墨堤";
    public string Version { get; }
    public string BrandLine => "墨堤是一座水墨的桥。声音从桥上过，小男孩在桥头等。";
    public string AuthorLine => "作者：Silvite";
    public string CopyrightLine => "© 2026 Silvite";
    public string LicenseAcknowledgement => "开源许可：GNU GPL v3";
    public string FontAcknowledgement => "霞鹜文楷：SIL Open Font License 1.1";

    public StoryCardViewModel Story { get; }
    public SupportCardViewModel Support { get; }
    public SponsorCardViewModel Sponsor { get; }
    public MarkdownDocumentViewModel ReleaseNotes { get; }
    public MarkdownDocumentViewModel ThirdPartyNotices { get; }
    public AsyncRelayCommand ContactCommand { get; }
    public AsyncRelayCommand ExportLogsCommand { get; }
    public AsyncRelayCommand CopyInfoCommand { get; }
    public RelayCommand ShowReleaseNotesCommand { get; }
    public RelayCommand ShowThirdPartyNoticesCommand { get; }
    public RelayCommand CloseDocumentCommand { get; }

    public MarkdownDocumentViewModel? ActiveDocument
    {
        get => _activeDocument;
        private set => SetProperty(ref _activeDocument, value);
    }

    public bool IsDocumentDialogOpen
    {
        get => _isDocumentDialogOpen;
        private set => SetProperty(ref _isDocumentDialogOpen, value);
    }

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
        if (_disposed)
            return;

        _disposed = true;
        ThirdPartyNotices.Dispose();
        ReleaseNotes.Dispose();
        Sponsor.Dispose();
        Support.Dispose();
        Story.Dispose();
        ContactCommand.RaiseCanExecuteChanged();
        ExportLogsCommand.RaiseCanExecuteChanged();
        CopyInfoCommand.RaiseCanExecuteChanged();
    }

    private async Task ContactAsync(CancellationToken cancellationToken) => await RunAsync(
        token => _navigation.OpenAsync(ExternalDestination.TechnicalSupport, token),
        "PRESENTATION_ABOUT_CONTACT",
        "无法打开技术支持页面",
        "技术支持入口已打开",
        cancellationToken);

    private async Task ExportLogsAsync(CancellationToken cancellationToken)
    {
        SetError(null, null);
        try
        {
            var result = await _logs.ExportAsync(cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                SetError(result.ErrorCode, result.UserMessage);
                return;
            }

            FeedbackText = $"已导出：{Path.GetFileName(result.Value.ArchiveDisplayName)}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError("PRESENTATION_ABOUT_LOG_EXPORT", "无法导出日志");
        }
    }

    private async Task CopyInfoAsync(CancellationToken cancellationToken) => await RunAsync(
        token => _clipboard.CopyTextAsync(BuildCopyText(), token),
        "PRESENTATION_ABOUT_COPY",
        "无法复制关于信息",
        "关于信息已复制",
        cancellationToken);

    private async Task RunAsync(
        Func<CancellationToken, Task<OperationResult>> operation,
        string exceptionCode,
        string exceptionMessage,
        string successMessage,
        CancellationToken cancellationToken)
    {
        SetError(null, null);
        try
        {
            var result = await operation(cancellationToken);
            if (!result.IsSuccess)
            {
                SetError(result.ErrorCode, result.UserMessage);
                return;
            }

            FeedbackText = successMessage;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError(exceptionCode, exceptionMessage);
        }
    }

    private void ShowDocument(MarkdownDocumentViewModel document)
    {
        ActiveDocument = document;
        IsDocumentDialogOpen = true;
    }

    private void CloseDocument()
    {
        IsDocumentDialogOpen = false;
        ActiveDocument = null;
    }

    private string BuildCopyText() => string.Join('\n',
        DisplayName,
        $"版本 {Version}",
        AuthorLine,
        LicenseAcknowledgement,
        FontAcknowledgement);

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}
