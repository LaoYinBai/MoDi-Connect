using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Settings;

public sealed class SettingsPageViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    public SettingsPageViewModel(
        StartupCardViewModel startup,
        ThemeCardViewModel theme,
        CustomAppearanceCardViewModel customAppearance,
        NetworkStatusCardViewModel networkStatus,
        PersonalizationResetCardViewModel personalizationReset,
        PluginManagerCardViewModel pluginManager,
        LogExportCardViewModel logExport)
    {
        Startup = startup ?? throw new ArgumentNullException(nameof(startup));
        Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        CustomAppearance = customAppearance ?? throw new ArgumentNullException(nameof(customAppearance));
        NetworkStatus = networkStatus ?? throw new ArgumentNullException(nameof(networkStatus));
        PersonalizationReset = personalizationReset ?? throw new ArgumentNullException(nameof(personalizationReset));
        PluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        LogExport = logExport ?? throw new ArgumentNullException(nameof(logExport));
    }

    public StartupCardViewModel Startup { get; }
    public ThemeCardViewModel Theme { get; }
    public CustomAppearanceCardViewModel CustomAppearance { get; }
    public NetworkStatusCardViewModel NetworkStatus { get; }
    public PersonalizationResetCardViewModel PersonalizationReset { get; }
    public PluginManagerCardViewModel PluginManager { get; }
    public LogExportCardViewModel LogExport { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        LogExport.Dispose();
        PluginManager.Dispose();
        PersonalizationReset.Dispose();
        NetworkStatus.Dispose();
        CustomAppearance.Dispose();
        Theme.Dispose();
        Startup.Dispose();
    }
}
