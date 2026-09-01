using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;
using MoDi.Desktop.Adapters;
using MoDi.Desktop.Platform.Appearance;
using MoDi.Desktop.Platform.Content;
using MoDi.Desktop.Platform.Features;
using MoDi.Desktop.Platform.Logging;
using MoDi.Desktop.Platform.Onboarding;
using MoDi.Desktop.Platform.Startup;
using MoDi.Desktop.Services;
using MoDi.Presentation.About;
using MoDi.Presentation.Markdown;
using MoDi.Presentation.Onboarding;
using MoDi.Presentation.P2p;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Shell;
using MoDi.Presentation.Stage;

namespace MoDi.Desktop.Composition;

public sealed class ProductionComposition : IDisposable
{
    private readonly IDisposable? _receiverOwner;
    private bool _disposed;

    private ProductionComposition(
        IReceiverRuntime runtime,
        ILocalAddressResolver localAddressResolver,
        IRegistryStore registryStore,
        ApplicationDataPaths paths,
        AppearanceService storedAppearance,
        IOnboardingService? onboardingService,
        IImageSelectionService imageSelection,
        IExternalNavigationService externalNavigation,
        IClipboardService clipboard,
        TimeProvider timeProvider,
        string executablePath,
        string? communityWebsite,
        IDisposable? receiverOwner,
        IStartupService? startupOverride,
        IAppearanceService? appearanceOverride,
        IMarkdownContentProvider? markdownOverride,
        ILogExportService? logExportOverride,
        IPluginCatalogService? pluginCatalogOverride)
    {
        _receiverOwner = receiverOwner;
        Receiver = new ReceiverStatusAdapter(runtime);
        Pairing = new PairingAdapter(runtime, timeProvider, payload => QrCodeHelper.GeneratePng(payload));
        Audio = new AudioSettingsAdapter(runtime);
        Network = new NetworkStatusAdapter(runtime, localAddressResolver);
        Appearance = appearanceOverride ?? storedAppearance;
        ImageSelection = imageSelection;
        Startup = startupOverride ?? new WindowsStartupService(registryStore, executablePath);
        PersonalizationReset = new PersonalizationResetService(storedAppearance);
        Logs = logExportOverride ?? new WindowsLogExportService(paths, timeProvider);
        Markdown = markdownOverride ?? new EmbeddedMarkdownContentProvider(typeof(ProductionComposition).Assembly);
        ExternalNavigation = externalNavigation;
        Clipboard = clipboard;

        var navigation = new NavigationViewModel();
        AudioFeature = new BuiltInAudioFeature(() => navigation.NavigateCommand.Execute(AppPage.Main));
        Plugins = pluginCatalogOverride ?? new BuiltInFeatureCatalogService([AudioFeature], ApplicationVersion());

        var topBar = new TopBarViewModel(navigation, Appearance);
        var featureRail = new FeatureRailViewModel(Appearance, [AudioFeature]);
        var statusBar = new StatusBarViewModel(Receiver, Audio);
        var stage = new BridgeStageViewModel(Receiver, Appearance, timeProvider);
        var pairedDevices = new PairedDevicesViewModel(Pairing);
        var qrPairing = new QrPairingViewModel(Pairing, timeProvider);
        var settings = new SettingsPageViewModel(
            new StartupCardViewModel(Startup),
            new ThemeCardViewModel(Appearance),
            new CustomAppearanceCardViewModel(Appearance, imageSelection),
            new NetworkStatusCardViewModel(Network),
            new PersonalizationResetCardViewModel(PersonalizationReset),
            new PluginManagerCardViewModel(Plugins),
            new LogExportCardViewModel(Logs));

        var releaseNotes = new MarkdownDocumentViewModel(Markdown, MarkdownContentKey.ReleaseNotes);
        var thirdPartyNotices = new MarkdownDocumentViewModel(Markdown, MarkdownContentKey.ThirdPartyNotices);
        var about = new AboutPageViewModel(
            new StoryCardViewModel(Markdown, MarkdownContentKey.Stories),
            new SupportCardViewModel(Markdown, MarkdownContentKey.TechnicalSupport, externalNavigation),
            new SponsorCardViewModel(Markdown, MarkdownContentKey.Sponsors, externalNavigation),
            releaseNotes,
            thirdPartyNotices,
            externalNavigation,
            clipboard,
            Logs,
            ApplicationDisplayVersion());

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
            about,
            onboardingService is null ? null : new OnboardingViewModel(onboardingService));
    }

    public ReceiverStatusAdapter Receiver { get; }
    public PairingAdapter Pairing { get; }
    public AudioSettingsAdapter Audio { get; }
    public NetworkStatusAdapter Network { get; }
    public IStartupService Startup { get; }
    public IAppearanceService Appearance { get; }
    public IImageSelectionService ImageSelection { get; }
    public PersonalizationResetService PersonalizationReset { get; }
    public ILogExportService Logs { get; }
    public IMarkdownContentProvider Markdown { get; }
    public IExternalNavigationService ExternalNavigation { get; }
    public IClipboardService Clipboard { get; }
    public BuiltInAudioFeature AudioFeature { get; }
    public IPluginCatalogService Plugins { get; }
    public AppShellViewModel Shell { get; }

    public static async Task<ProductionComposition> CreateAsync(
        ProductionHostContext hostContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostContext);
        var controller = new ReceiverController();
        var paths = ApplicationDataPaths.CreateDefault();
        var destinations = BuildDestinations(hostContext.CommunityWebsiteUrl);
        var externalNavigation = new WindowsExternalNavigationService(destinations);
        var executablePath = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "MoDi.Desktop.exe");
        var appearance = await AppearanceService.CreateAsync(
            paths,
            TimeProvider.System,
            cancellationToken).ConfigureAwait(false);
        var onboarding = await WindowsOnboardingService.CreateAsync(
            paths,
            TimeProvider.System,
            cancellationToken).ConfigureAwait(false);
        return new ProductionComposition(
            new ReceiverRuntime(controller),
            new LocalAddressResolver(),
            new RegistryStore(),
            paths,
            appearance,
            onboarding,
            new WindowsImageSelectionService(hostContext.StorageProviderAccessor),
            externalNavigation,
            new AvaloniaClipboardService(hostContext.ClipboardAccessor),
            TimeProvider.System,
            executablePath,
            hostContext.CommunityWebsiteUrl,
            controller,
            startupOverride: null,
            appearanceOverride: null,
            markdownOverride: null,
            logExportOverride: null,
            pluginCatalogOverride: null);
    }

    internal static ProductionComposition CreateForTest(TestProductionDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        var paths = new ApplicationDataPaths(dependencies.ApplicationDataRoot);
        return new ProductionComposition(
            dependencies.ReceiverRuntime,
            dependencies.LocalAddressResolver,
            dependencies.RegistryStore,
            paths,
            new AppearanceService(paths),
            onboardingService: null,
            dependencies.ImageSelection,
            dependencies.ExternalNavigation,
            dependencies.Clipboard,
            dependencies.TimeProvider,
            Path.Combine(paths.RootDirectory, "MoDi.Desktop.exe"),
            communityWebsite: null,
            receiverOwner: null,
            dependencies.StartupOverride,
            dependencies.AppearanceOverride,
            dependencies.MarkdownOverride,
            dependencies.LogExportOverride,
            dependencies.PluginCatalogOverride);
    }

    public async Task<OperationResult> InitializeAsync(CancellationToken cancellationToken)
    {
        var receiverResult = await Receiver.InitializeAsync(cancellationToken);
        Shell.Onboarding?.ShowIfIncomplete();
        await LoadPackagedContentAsync(cancellationToken);
        return receiverResult;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Shell.Dispose();
        Network.Dispose();
        Audio.Dispose();
        Pairing.Dispose();
        Receiver.Dispose();
        _receiverOwner?.Dispose();
    }

    private async Task LoadPackagedContentAsync(CancellationToken cancellationToken)
    {
        var about = Shell.About;
        await about.Story.Content.LoadCommand.ExecuteAsync(cancellationToken);
        await about.Support.Content.LoadCommand.ExecuteAsync(cancellationToken);
        await about.Sponsor.Content.LoadCommand.ExecuteAsync(cancellationToken);
        await about.ReleaseNotes.LoadCommand.ExecuteAsync(cancellationToken);
        await about.ThirdPartyNotices.LoadCommand.ExecuteAsync(cancellationToken);
    }

    internal static Dictionary<ExternalDestination, Uri> BuildDestinations(string? communityWebsite)
    {
        var destinations = new Dictionary<ExternalDestination, Uri>
        {
            [ExternalDestination.SponsorPage] = new("https://ifdian.net/a/modiconnect"),
        };
        if (Uri.TryCreate(communityWebsite, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            destinations[ExternalDestination.CommunityWebsite] = uri;
        return destinations;
    }

    private static string ApplicationVersion()
    {
        var assembly = typeof(ProductionComposition).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational?.Split('+', 2)[0] ?? assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string ApplicationDisplayVersion()
    {
        var assembly = typeof(ProductionComposition).Assembly;
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        var build = metadata.FirstOrDefault(value => value.Key == "MoDiBuild")?.Value ?? "unknown";
        var commit = metadata.FirstOrDefault(value => value.Key == "MoDiCommit")?.Value ?? "unknown";
        return $"{ApplicationVersion()} · Build {build} · {commit}";
    }
}
