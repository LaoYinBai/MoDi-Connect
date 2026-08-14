using System.ComponentModel;
using MoDi.App.Contracts;
using MoDi.Presentation.About;
using MoDi.Presentation.Infrastructure;
using MoDi.Presentation.P2p;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Stage;

namespace MoDi.Presentation.Shell;

public sealed class AppShellViewModel : ObservableObject, IDisposable
{
    private readonly IAppearanceService _appearanceService;
    private AppearanceSnapshot _appearance;
    private bool _disposed;

    public AppShellViewModel(
        IAppearanceService appearance,
        NavigationViewModel navigation,
        TopBarViewModel topBar,
        FeatureRailViewModel featureRail,
        StatusBarViewModel statusBar,
        BridgeStageViewModel stage,
        PairedDevicesViewModel pairedDevices,
        QrPairingViewModel qrPairing,
        SettingsPageViewModel settings,
        AboutPageViewModel about)
    {
        _appearanceService = appearance ?? throw new ArgumentNullException(nameof(appearance));
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        TopBar = topBar ?? throw new ArgumentNullException(nameof(topBar));
        FeatureRail = featureRail ?? throw new ArgumentNullException(nameof(featureRail));
        StatusBar = statusBar ?? throw new ArgumentNullException(nameof(statusBar));
        Stage = stage ?? throw new ArgumentNullException(nameof(stage));
        PairedDevices = pairedDevices ?? throw new ArgumentNullException(nameof(pairedDevices));
        QrPairing = qrPairing ?? throw new ArgumentNullException(nameof(qrPairing));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        About = about ?? throw new ArgumentNullException(nameof(about));
        _appearance = appearance.Snapshot;

        Navigation.PropertyChanged += OnNavigationChanged;
        PairedDevices.PairNewDeviceRequested += OnPairNewDeviceRequested;
        _appearanceService.SnapshotChanged += OnAppearanceChanged;
    }

    public NavigationViewModel Navigation { get; }
    public TopBarViewModel TopBar { get; }
    public FeatureRailViewModel FeatureRail { get; }
    public StatusBarViewModel StatusBar { get; }
    public BridgeStageViewModel Stage { get; }
    public PairedDevicesViewModel PairedDevices { get; }
    public QrPairingViewModel QrPairing { get; }
    public SettingsPageViewModel Settings { get; }
    public AboutPageViewModel About { get; }

    public AppearanceSnapshot Appearance
    {
        get => _appearance;
        private set => SetProperty(ref _appearance, value);
    }

    public object CurrentPageViewModel => Navigation.CurrentPage switch
    {
        AppPage.Settings => Settings,
        AppPage.About => About,
        _ => this,
    };

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _appearanceService.SnapshotChanged -= OnAppearanceChanged;
        PairedDevices.PairNewDeviceRequested -= OnPairNewDeviceRequested;
        Navigation.PropertyChanged -= OnNavigationChanged;
        About.Dispose();
        Settings.Dispose();
        QrPairing.Dispose();
        PairedDevices.Dispose();
        Stage.Dispose();
        StatusBar.Dispose();
        FeatureRail.Dispose();
        TopBar.Dispose();
    }

    private void OnNavigationChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(NavigationViewModel.CurrentPage))
            OnPropertyChanged(nameof(CurrentPageViewModel));
    }

    private void OnPairNewDeviceRequested(object? sender, EventArgs eventArgs) => QrPairing.Open();

    private void OnAppearanceChanged(AppearanceSnapshot snapshot) => Appearance = snapshot;
}
