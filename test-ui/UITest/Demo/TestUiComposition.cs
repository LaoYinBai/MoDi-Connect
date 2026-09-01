using System;
using MoDi.App.Contracts;
using MoDi.Presentation.About;
using MoDi.Presentation.Markdown;
using MoDi.Presentation.P2p;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Shell;
using MoDi.Presentation.Stage;
using UITest.Fakes;

namespace UITest.Demo;

public sealed class TestUiComposition : IDisposable
{
    public TestUiComposition(TimeProvider timeProvider)
    {
        Appearance = new FakeAppearanceService();
        Receiver = new FakeReceiverStatusSource();
        Pairing = new FakePairingService(timeProvider);
        Audio = new FakeAudioSettingsService();
        Network = new FakeNetworkStatusSource();
        Startup = new FakeStartupService();
        Images = new FakeImageSelectionService();
        Reset = new FakePersonalizationResetService(Appearance);
        Plugins = new FakePluginCatalogService();
        Logs = new FakeLogExportService();
        Markdown = new FakeMarkdownContentProvider();
        ExternalNavigation = new FakeExternalNavigationService();
        Clipboard = new FakeClipboardService();
        AudioFeature = new FakeBuiltInAudioFeature();

        var navigation = new NavigationViewModel();
        var topBar = new TopBarViewModel(navigation, Appearance);
        var featureRail = new FeatureRailViewModel(Appearance, [AudioFeature]);
        var statusBar = new StatusBarViewModel(Receiver, Audio);
        var stage = new BridgeStageViewModel(Receiver, Appearance, timeProvider);
        var pairedDevices = new PairedDevicesViewModel(Pairing);
        var qrPairing = new QrPairingViewModel(Pairing, timeProvider);
        var settings = new SettingsPageViewModel(
            new StartupCardViewModel(Startup),
            new ThemeCardViewModel(Appearance),
            new CustomAppearanceCardViewModel(Appearance, Images),
            new NetworkStatusCardViewModel(Network),
            new PersonalizationResetCardViewModel(Reset),
            new PluginManagerCardViewModel(Plugins),
            new LogExportCardViewModel(Logs));
        var about = new AboutPageViewModel(
            Markdown,
            ExternalNavigation,
            Clipboard,
            Logs,
            typeof(AboutPageViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

        Shell = new AppShellViewModel(
            Appearance,
            navigation,
            topBar,
            featureRail,
            statusBar,
            stage,
            pairedDevices,
            qrPairing,
            settings,
            about);
        Demo = new DemoControlsViewModel(Receiver, Appearance, Pairing, Plugins, timeProvider);
        LoadDemoContent(about);
    }

    public AppShellViewModel Shell { get; }
    public DemoControlsViewModel Demo { get; }
    public FakeReceiverStatusSource Receiver { get; }
    public FakePairingService Pairing { get; }
    public FakeAudioSettingsService Audio { get; }
    public FakeNetworkStatusSource Network { get; }
    public FakeAppearanceService Appearance { get; }
    public FakeStartupService Startup { get; }
    public FakeImageSelectionService Images { get; }
    public FakePersonalizationResetService Reset { get; }
    public FakePluginCatalogService Plugins { get; }
    public FakeLogExportService Logs { get; }
    public FakeMarkdownContentProvider Markdown { get; }
    public FakeExternalNavigationService ExternalNavigation { get; }
    public FakeClipboardService Clipboard { get; }
    public FakeBuiltInAudioFeature AudioFeature { get; }

    public void Dispose()
    {
        Demo.Dispose();
        Shell.Dispose();
        Pairing.Dispose();
        Network.Dispose();
        Audio.Dispose();
        Receiver.Dispose();
    }

    private static void LoadDemoContent(AboutPageViewModel about)
    {
        about.PreloadAsync(default).GetAwaiter().GetResult();
    }
}
