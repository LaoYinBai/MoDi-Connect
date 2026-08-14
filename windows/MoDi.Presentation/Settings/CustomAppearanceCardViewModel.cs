using System.Text.RegularExpressions;
using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Settings;

public sealed partial class CustomAppearanceCardViewModel : ObservableObject, IDisposable
{
    public const double MinimumRailWidth = 56d;
    public const double MaximumRailWidth = 200d;

    private readonly IAppearanceService _appearance;
    private readonly IImageSelectionService _imageSelection;
    private string _background = string.Empty;
    private string _surface = string.Empty;
    private string _surfaceElevated = string.Empty;
    private string _textPrimary = string.Empty;
    private string _textSecondary = string.Empty;
    private string _accent = string.Empty;
    private string _border = string.Empty;
    private string _success = string.Empty;
    private string _backgroundDisplayName = "未选择背景图";
    private bool _reduceMotion;
    private double _featureRailWidth;
    private string? _errorCode;
    private string? _errorMessage;
    private string? _feedbackText;
    private bool _disposed;

    public CustomAppearanceCardViewModel(
        IAppearanceService appearance,
        IImageSelectionService imageSelection)
    {
        _appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        _imageSelection = imageSelection ?? throw new ArgumentNullException(nameof(imageSelection));
        SavePaletteCommand = new AsyncRelayCommand(SavePaletteAsync);
        SelectBackgroundCommand = new AsyncRelayCommand(SelectBackgroundAsync);
        ToggleReduceMotionCommand = new AsyncRelayCommand(ToggleReduceMotionAsync);
        SaveRailWidthCommand = new AsyncRelayCommand(SaveRailWidthAsync);
        ApplySnapshot(appearance.Snapshot);
        appearance.SnapshotChanged += OnSnapshotChanged;
    }

    public AsyncRelayCommand SavePaletteCommand { get; }
    public AsyncRelayCommand SelectBackgroundCommand { get; }
    public AsyncRelayCommand ToggleReduceMotionCommand { get; }
    public AsyncRelayCommand SaveRailWidthCommand { get; }

    public string Background { get => _background; set => SetProperty(ref _background, value); }
    public string Surface { get => _surface; set => SetProperty(ref _surface, value); }
    public string SurfaceElevated { get => _surfaceElevated; set => SetProperty(ref _surfaceElevated, value); }
    public string TextPrimary { get => _textPrimary; set => SetProperty(ref _textPrimary, value); }
    public string TextSecondary { get => _textSecondary; set => SetProperty(ref _textSecondary, value); }
    public string Accent { get => _accent; set => SetProperty(ref _accent, value); }
    public string Border { get => _border; set => SetProperty(ref _border, value); }
    public string Success { get => _success; set => SetProperty(ref _success, value); }

    public string BackgroundDisplayName
    {
        get => _backgroundDisplayName;
        private set => SetProperty(ref _backgroundDisplayName, value);
    }

    public bool ReduceMotion
    {
        get => _reduceMotion;
        private set => SetProperty(ref _reduceMotion, value);
    }

    public double FeatureRailWidth
    {
        get => _featureRailWidth;
        set => SetProperty(ref _featureRailWidth, Math.Clamp(value, MinimumRailWidth, MaximumRailWidth));
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
    public string? FeedbackText { get => _feedbackText; private set => SetProperty(ref _feedbackText, value); }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _appearance.SnapshotChanged -= OnSnapshotChanged;
    }

    private async Task SavePaletteAsync(CancellationToken cancellationToken)
    {
        var values = new[] { Background, Surface, SurfaceElevated, TextPrimary, TextSecondary, Accent, Border, Success };
        if (values.Any(value => !RgbColorPattern().IsMatch(value ?? string.Empty)))
        {
            SetError("APPEARANCE_COLOR_INVALID", "颜色必须使用 #RRGGBB 格式");
            return;
        }

        await RunOperationAsync(
            token => _appearance.SaveCustomPaletteAsync(new CustomPalette(
                Background.ToUpperInvariant(), Surface.ToUpperInvariant(), SurfaceElevated.ToUpperInvariant(),
                TextPrimary.ToUpperInvariant(), TextSecondary.ToUpperInvariant(), Accent.ToUpperInvariant(),
                Border.ToUpperInvariant(), Success.ToUpperInvariant()), token),
            "PRESENTATION_CUSTOM_APPEARANCE",
            "无法保存自定义颜色",
            "自定义颜色已保存",
            cancellationToken);
    }

    private async Task SelectBackgroundAsync(CancellationToken cancellationToken)
    {
        SetError(null, null);
        try
        {
            var selected = await _imageSelection.SelectImageAsync(cancellationToken);
            if (!selected.IsSuccess || selected.Value is null)
            {
                SetError(selected.ErrorCode, selected.UserMessage);
                return;
            }

            var imported = await _appearance.ImportBackgroundAsync(selected.Value, cancellationToken);
            if (!imported.IsSuccess)
            {
                SetError(imported.ErrorCode, imported.UserMessage);
                return;
            }

            FeedbackText = $"已选择：{selected.Value.DisplayName}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError("PRESENTATION_BACKGROUND_IMAGE", "无法导入背景图片");
        }
    }

    private Task ToggleReduceMotionAsync(CancellationToken cancellationToken) => RunOperationAsync(
        token => _appearance.SetReduceMotionAsync(!ReduceMotion, token),
        "PRESENTATION_REDUCE_MOTION",
        "无法更改动效设置",
        "动效设置已保存",
        cancellationToken);

    private Task SaveRailWidthAsync(CancellationToken cancellationToken) => RunOperationAsync(
        token => _appearance.SetFeatureRailWidthAsync(
            Math.Clamp(FeatureRailWidth, MinimumRailWidth, MaximumRailWidth), token),
        "PRESENTATION_RAIL_WIDTH",
        "无法保存左栏宽度",
        "左栏宽度已保存",
        cancellationToken);

    private async Task RunOperationAsync(
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

    private void OnSnapshotChanged(AppearanceSnapshot snapshot) => ApplySnapshot(snapshot);

    private void ApplySnapshot(AppearanceSnapshot snapshot)
    {
        if (_disposed)
            return;

        Background = snapshot.Palette.Background;
        Surface = snapshot.Palette.Surface;
        SurfaceElevated = snapshot.Palette.SurfaceElevated;
        TextPrimary = snapshot.Palette.TextPrimary;
        TextSecondary = snapshot.Palette.TextSecondary;
        Accent = snapshot.Palette.Accent;
        Border = snapshot.Palette.Border;
        Success = snapshot.Palette.Success;
        BackgroundDisplayName = snapshot.BackgroundDisplayName ?? "未选择背景图";
        ReduceMotion = snapshot.ReduceMotion;
        FeatureRailWidth = snapshot.FeatureRailWidth;
    }

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex RgbColorPattern();
}
