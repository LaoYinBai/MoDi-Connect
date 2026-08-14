using MoDi.Presentation.Settings;

namespace MoDi.Presentation.Tests.TestDoubles;

internal static class PresentationTestFactory
{
    public static SettingsPageViewModel CreateSettingsPage()
    {
        var appearance = new RecordingAppearanceService();
        return new SettingsPageViewModel(
            new StartupCardViewModel(new RecordingStartupService()),
            new ThemeCardViewModel(appearance),
            new CustomAppearanceCardViewModel(appearance, new RecordingImageSelectionService()),
            new NetworkStatusCardViewModel(new RecordingNetworkStatusSource()),
            new PersonalizationResetCardViewModel(new RecordingPersonalizationResetService()),
            new PluginManagerCardViewModel(new RecordingPluginCatalogService()),
            new LogExportCardViewModel(new RecordingLogExportService()));
    }
}
