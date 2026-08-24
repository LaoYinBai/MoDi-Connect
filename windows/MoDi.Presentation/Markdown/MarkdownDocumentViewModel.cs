using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Markdown;

public sealed class MarkdownDocumentViewModel : ObservableObject, IDisposable
{
    private readonly IMarkdownContentProvider _provider;
    private MarkdownDocument _document = MarkdownDocument.Empty;
    private bool _isLoading;
    private bool _isLoaded;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public MarkdownDocumentViewModel(IMarkdownContentProvider provider, MarkdownContentKey key)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Key = key;
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !_disposed);
    }

    public MarkdownContentKey Key { get; }
    public string Title => Key switch
    {
        MarkdownContentKey.Stories => "故事汇",
        MarkdownContentKey.StoryOrigin => "为什么叫墨堤",
        MarkdownContentKey.StoryCurrentChapter => "当前这一章",
        MarkdownContentKey.StoryInkBridge => "水墨桥的语法",
        MarkdownContentKey.TechnicalSupport => "技术支持",
        MarkdownContentKey.SupportUpdates => "版本与更新",
        MarkdownContentKey.SupportConnections => "连接排查",
        MarkdownContentKey.SupportDiagnostics => "日志与诊断",
        MarkdownContentKey.Sponsors => "赞助列表",
        MarkdownContentKey.ReleaseNotes => "发行说明",
        MarkdownContentKey.ThirdPartyNotices => "第三方声明",
        _ => Key.ToString(),
    };
    public AsyncRelayCommand LoadCommand { get; }

    public MarkdownDocument Document
    {
        get => _document;
        private set => SetProperty(ref _document, value);
    }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsLoaded { get => _isLoaded; private set => SetProperty(ref _isLoaded, value); }
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
        LoadCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        SetError(null, null);
        try
        {
            var content = await _provider.GetAsync(Key, cancellationToken);
            if (!content.IsSuccess || content.Value is null)
            {
                SetError(content.ErrorCode, content.UserMessage);
                return;
            }

            var parsed = SafeMarkdownParser.Parse(content.Value);
            if (!parsed.IsSuccess || parsed.Value is null)
            {
                SetError(parsed.ErrorCode, parsed.UserMessage);
                return;
            }

            Document = parsed.Value;
            IsLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError("PRESENTATION_MARKDOWN_LOAD", "无法加载内置内容");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}
