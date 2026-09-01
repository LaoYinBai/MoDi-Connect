using MoDi.Desktop.Adapters;
using MoDi.Desktop.Composition;
using MoDi.Desktop.Platform.Appearance;
using MoDi.Desktop.Platform.Startup;
using MoDi.Desktop.Tests.Adapters;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Composition;

public sealed class ProductionCompositionTests
{
    [Fact]
    public void Production_composition_builds_each_module_from_real_contracts()
    {
        using var temp = TempDirectory.Create();
        using var composition = Create(temp.Path, out _);

        Assert.IsType<ReceiverStatusAdapter>(composition.Receiver);
        Assert.IsType<PairingAdapter>(composition.Pairing);
        Assert.IsType<AudioSettingsAdapter>(composition.Audio);
        Assert.IsType<NetworkStatusAdapter>(composition.Network);
        Assert.IsType<WindowsStartupService>(composition.Startup);
        Assert.IsType<AppearanceService>(composition.Appearance);
        Assert.NotNull(composition.Shell.Settings);
        Assert.NotNull(composition.Shell.About);
    }

    [Fact]
    public async Task Initialize_starts_receiver_once_and_loads_all_packaged_markdown()
    {
        using var temp = TempDirectory.Create();
        using var composition = Create(temp.Path, out var runtime);

        var result = await composition.InitializeAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, runtime.InitializeCalls);
        Assert.True(composition.Shell.About.Stories.SelectedItem?.Document.IsLoaded);
        Assert.True(composition.Shell.About.SupportLibrary.SelectedItem?.Document.IsLoaded);
        Assert.True(composition.Shell.About.Sponsors.SelectedItem?.Document.IsLoaded);
        Assert.True(composition.Shell.About.ReleaseNotes.IsLoaded);
        Assert.True(composition.Shell.About.ThirdPartyNotices.IsLoaded);
    }

    [Fact]
    public void Sponsor_destination_defaults_to_the_official_Afdian_page()
    {
        var destinations = ProductionComposition.BuildDestinations("https://modiconnect.cn");

        Assert.Equal(
            new Uri("https://ifdian.net/a/modiconnect"),
            destinations[MoDi.App.Contracts.ExternalDestination.SponsorPage]);
    }

    private static ProductionComposition Create(string root, out TestReceiverRuntime runtime)
    {
        runtime = new TestReceiverRuntime();
        return ProductionComposition.CreateForTest(new TestProductionDependencies(
            runtime,
            new FixedLocalAddressResolver("192.168.1.20"),
            new MemoryRegistryStore(),
            root,
            new RecordingImageSelectionService(),
            new RecordingExternalNavigationService(),
            new RecordingClipboardService(),
            TimeProvider.System));
    }
}
