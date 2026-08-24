using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.About;

public sealed class SupportCardViewModel : ObservableObject, IDisposable
{
    private readonly IExternalNavigationService _navigation;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public SupportCardViewModel(
        IExternalNavigationService navigation,
        Action openLibrary)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        OpenLibraryCommand = new RelayCommand(openLibrary);
        OpenSupportCommand = new AsyncRelayCommand(OpenSupportAsync, () => !_disposed);
    }

    public string Title => "技术支持";
    public string LatestTitle => "版本与更新";
    public string Preview => "查看更新、连接排查、日志导出与常见错误的离线说明。";
    public string CountText => "共 3 类";
    public RelayCommand OpenLibraryCommand { get; }
    public AsyncRelayCommand OpenSupportCommand { get; }
    public string? ErrorCode { get => _errorCode; private set => SetProperty(ref _errorCode, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public void Dispose()
    {
        _disposed = true;
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
