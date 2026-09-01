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
    private ContentLibraryViewModel? _activeLibrary;
    private bool _isDocumentDialogOpen;
    private bool _isLibraryDialogOpen;
    private string? _feedbackText;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public AboutPageViewModel(
        IMarkdownContentProvider provider,
        IExternalNavigationService navigation,
        IClipboardService clipboard,
        ILogExportService logs,
        string version)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        Version = string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;

        Stories = new ContentLibraryViewModel("故事汇",
        [
            Item(provider, MarkdownContentKey.StoryOrigin, "为什么叫墨堤", "墨与堤如何成为产品的名字。"),
            Item(provider, MarkdownContentKey.StoryCurrentChapter, "当前这一章", "V1 如何先把声音稳稳送过桥。"),
            Item(provider, MarkdownContentKey.StoryInkBridge, "水墨桥的语法", "状态、动画与等待如何构成墨堤。"),
        ]);
        SupportLibrary = new ContentLibraryViewModel("技术支持",
        [
            Item(provider, MarkdownContentKey.SupportUpdates, "版本与更新", "自动检查、增量更新、完整包与回滚。"),
            Item(provider, MarkdownContentKey.SupportConnections, "连接排查", "局域网、蓝牙、USB 与 Wi-Fi Direct 排查。"),
            Item(provider, MarkdownContentKey.SupportDiagnostics, "日志与诊断", "安全导出日志并提供可复现信息。"),
        ]);
        Sponsors = new ContentLibraryViewModel("赞助名单",
        [
            Item(provider, MarkdownContentKey.Sponsors, "全部赞助名单", "查看所有同意公开展示的赞助者。"),
        ]);
        Story = new StoryCardViewModel(() => ShowLibrary(Stories));
        Support = new SupportCardViewModel(navigation, () => ShowLibrary(SupportLibrary));
        Sponsor = new SponsorCardViewModel(navigation, () => ShowLibrary(Sponsors));
        ReleaseNotes = new MarkdownDocumentViewModel(provider, MarkdownContentKey.ReleaseNotes);
        ThirdPartyNotices = new MarkdownDocumentViewModel(provider, MarkdownContentKey.ThirdPartyNotices);

        ContactCommand = new AsyncRelayCommand(ContactAsync, () => !_disposed);
        ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync, () => !_disposed);
        CopyInfoCommand = new AsyncRelayCommand(CopyInfoAsync, () => !_disposed);
        ShowReleaseNotesCommand = new RelayCommand(() => ShowDocument(ReleaseNotes));
        ShowThirdPartyNoticesCommand = new RelayCommand(() => ShowDocument(ThirdPartyNotices));
        CloseDocumentCommand = new RelayCommand(CloseDocument);
        CloseLibraryCommand = new RelayCommand(CloseLibrary);
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
    public ContentLibraryViewModel Stories { get; }
    public ContentLibraryViewModel SupportLibrary { get; }
    public ContentLibraryViewModel Sponsors { get; }
    public MarkdownDocumentViewModel ReleaseNotes { get; }
    public MarkdownDocumentViewModel ThirdPartyNotices { get; }
    public AsyncRelayCommand ContactCommand { get; }
    public AsyncRelayCommand ExportLogsCommand { get; }
    public AsyncRelayCommand CopyInfoCommand { get; }
    public RelayCommand ShowReleaseNotesCommand { get; }
    public RelayCommand ShowThirdPartyNoticesCommand { get; }
    public RelayCommand CloseDocumentCommand { get; }
    public RelayCommand CloseLibraryCommand { get; }

    public ContentLibraryViewModel? ActiveLibrary
    {
        get => _activeLibrary;
        private set => SetProperty(ref _activeLibrary, value);
    }

    public bool IsLibraryDialogOpen
    {
        get => _isLibraryDialogOpen;
        private set => SetProperty(ref _isLibraryDialogOpen, value);
    }

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
        Sponsors.Dispose();
        SupportLibrary.Dispose();
        Stories.Dispose();
        Sponsor.Dispose();
        Support.Dispose();
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
                if (result.ErrorCode == "LOG_EXPORT_CANCELLED")
                {
                    FeedbackText = "已取消导出";
                    return;
                }
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

    public async Task PreloadAsync(CancellationToken cancellationToken)
    {
        await Stories.LoadSelectedAsync(cancellationToken);
        await SupportLibrary.LoadSelectedAsync(cancellationToken);
        await Sponsors.LoadSelectedAsync(cancellationToken);
        await ReleaseNotes.LoadCommand.ExecuteAsync(cancellationToken);
        await ThirdPartyNotices.LoadCommand.ExecuteAsync(cancellationToken);
    }

    private void ShowLibrary(ContentLibraryViewModel library)
    {
        ActiveLibrary = library;
        IsLibraryDialogOpen = true;
        _ = library.LoadSelectedAsync();
    }

    private void CloseLibrary()
    {
        IsLibraryDialogOpen = false;
        ActiveLibrary = null;
    }

    private static ContentLibraryItemViewModel Item(
        IMarkdownContentProvider provider,
        MarkdownContentKey key,
        string title,
        string summary) => new(title, summary, new MarkdownDocumentViewModel(provider, key));

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
