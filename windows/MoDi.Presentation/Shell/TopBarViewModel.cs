using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Shell;

public sealed class TopBarViewModel : ObservableObject, IDisposable
{
    private readonly IAppearanceService _appearance;
    private ThemePreset _preset;
    private bool _disposed;

    public TopBarViewModel(NavigationViewModel navigation, IAppearanceService appearance)
    {
        Navigation = navigation;
        _appearance = appearance;
        _preset = appearance.Snapshot.Preset;
        ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);
        _appearance.SnapshotChanged += OnAppearanceChanged;
    }

    public NavigationViewModel Navigation { get; }
    public AsyncRelayCommand ToggleThemeCommand { get; }
    public bool ShowSwitchToLightTheme => _preset != ThemePreset.PaperDay;
    public bool ShowSwitchToDarkTheme => _preset == ThemePreset.PaperDay;

    private async Task ToggleThemeAsync(CancellationToken cancellationToken)
    {
        var target = _preset == ThemePreset.PaperDay ? ThemePreset.InkNight : ThemePreset.PaperDay;
        await _appearance.SelectPresetAsync(target, cancellationToken);
    }

    private void OnAppearanceChanged(AppearanceSnapshot snapshot)
    {
        if (_preset == snapshot.Preset)
            return;
        _preset = snapshot.Preset;
        OnPropertyChanged(nameof(ShowSwitchToLightTheme));
        OnPropertyChanged(nameof(ShowSwitchToDarkTheme));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _appearance.SnapshotChanged -= OnAppearanceChanged;
    }
}
