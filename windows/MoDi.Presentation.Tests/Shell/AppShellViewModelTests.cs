using MoDi.App.Contracts;
using MoDi.Presentation.About;
using MoDi.Presentation.Markdown;
using MoDi.Presentation.P2p;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Shell;
using MoDi.Presentation.Stage;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Shell;

public sealed class AppShellViewModelTests
{
    [Fact]
    public void Navigation_selects_only_the_composed_page_view_model()
    {
        using var vm = CreateShell();

        Assert.Same(vm, vm.CurrentPageViewModel);

        vm.Navigation.NavigateCommand.Execute(AppPage.Settings);
        Assert.Same(vm.Settings, vm.CurrentPageViewModel);

        vm.Navigation.NavigateCommand.Execute(AppPage.About);
        Assert.Same(vm.About, vm.CurrentPageViewModel);
    }

    [Fact]
    public void Pair_new_device_request_is_brokered_without_pairing_modules_referencing_each_other()
    {
        using var vm = CreateShell();

        vm.PairedDevices.PairNewDeviceCommand.Execute(null);

        Assert.True(vm.QrPairing.IsOpen);
        Assert.DoesNotContain(vm.PairedDevices.GetType().GetProperties(), property => property.PropertyType == typeof(QrPairingViewModel));
        Assert.DoesNotContain(vm.QrPairing.GetType().GetProperties(), property => property.PropertyType == typeof(PairedDevicesViewModel));
    }

    [Fact]
    public void Appearance_snapshot_is_exposed_for_the_view_resource_applicator()
    {
        var appearance = new RecordingAppearanceService();
        using var vm = CreateShell(appearance);
        var paper = appearance.Snapshot with { Preset = ThemePreset.PaperDay };

        appearance.Publish(paper);

        Assert.Same(paper, vm.Appearance);
    }

    private static AppShellViewModel CreateShell(RecordingAppearanceService? appearance = null)
    {
        appearance ??= new RecordingAppearanceService();
        var receiver = new RecordingReceiverStatusSource();
        var audio = new RecordingAudioSettingsService();
        var pairing = new RecordingPairingService();
        var navigation = new NavigationViewModel();
        var provider = new RecordingMarkdownContentProvider();
        var external = new RecordingExternalNavigationService();
        var settings = PresentationTestFactory.CreateSettingsPage();
        var about = new AboutPageViewModel(
            provider,
            external,
            new RecordingClipboardService(),
            new RecordingLogExportService(),
            "1.0.0");
        return new AppShellViewModel(
            appearance,
            navigation,
            new TopBarViewModel(navigation, appearance),
            new FeatureRailViewModel(appearance, [new StubBuiltInFeature()]),
            new StatusBarViewModel(receiver, audio),
            new BridgeStageViewModel(receiver, appearance, TimeProvider.System),
            new PairedDevicesViewModel(pairing),
            new QrPairingViewModel(pairing, TimeProvider.System),
            settings,
            about);
    }
}
