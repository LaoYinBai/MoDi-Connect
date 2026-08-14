using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Settings;

public sealed class ThemeCardViewModel : ObservableObject, IDisposable
{
    private readonly IAppearanceService _appearance;
    private ThemePreset _selectedPreset;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public ThemeCardViewModel(IAppearanceService appearance)
    {
        _appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        Options =
        [
            new ThemeOptionViewModel(ThemePreset.InkNight, "墨·夜堤", "深色水墨"),
            new ThemeOptionViewModel(ThemePreset.PaperDay, "宣纸·昼堤", "浅色宣纸"),
            new ThemeOptionViewModel(ThemePreset.Custom, "自定义", "使用下方调色盘")
        ];
        SelectCommand = new AsyncRelayCommand<ThemeOptionViewModel>(SelectAsync, option => option is not null && !_disposed);
        ApplySnapshot(appearance.Snapshot);
        appearance.SnapshotChanged += OnSnapshotChanged;
    }

    public IReadOnlyList<ThemeOptionViewModel> Options { get; }
    public AsyncRelayCommand<ThemeOptionViewModel> SelectCommand { get; }

    public ThemePreset SelectedPreset
    {
        get => _selectedPreset;
        private set
        {
            if (!SetProperty(ref _selectedPreset, value))
                return;

            OnPropertyChanged(nameof(SelectedDisplayName));
            foreach (var option in Options)
                option.SetSelected(option.Preset == value);
        }
    }

    public string SelectedDisplayName =>
        Options.First(option => option.Preset == SelectedPreset).DisplayName;

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
        _appearance.SnapshotChanged -= OnSnapshotChanged;
        SelectCommand.RaiseCanExecuteChanged();
    }

    private async Task SelectAsync(ThemeOptionViewModel? option, CancellationToken cancellationToken)
    {
        if (option is null)
            return;

        SetError(null, null);
        try
        {
            var result = await _appearance.SelectPresetAsync(option.Preset, cancellationToken);
            if (!result.IsSuccess)
                SetError(result.ErrorCode, result.UserMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError("PRESENTATION_THEME", "无法切换主题，请稍后重试");
        }
    }

    private void OnSnapshotChanged(AppearanceSnapshot snapshot) => ApplySnapshot(snapshot);

    private void ApplySnapshot(AppearanceSnapshot snapshot)
    {
        if (_disposed)
            return;

        SelectedPreset = snapshot.Preset;
        foreach (var option in Options)
            option.SetSelected(option.Preset == snapshot.Preset);
    }

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}

public sealed class ThemeOptionViewModel : ObservableObject
{
    private bool _isSelected;

    internal ThemeOptionViewModel(ThemePreset preset, string displayName, string description)
    {
        Preset = preset;
        DisplayName = displayName;
        Description = description;
    }

    public ThemePreset Preset { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public bool IsSelected
    {
        get => _isSelected;
        private set => SetProperty(ref _isSelected, value);
    }

    internal void SetSelected(bool isSelected) => IsSelected = isSelected;
}
