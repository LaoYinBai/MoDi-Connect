using MoDi.App.Contracts;
using MoDi.Desktop.Composition;
using MoDi.Desktop.Tests.Adapters;
using MoDi.Desktop.Tests.TestDoubles;
using MoDi.Presentation.Shell;
using Xunit;

namespace MoDi.Desktop.Tests.Composition;

public sealed class FaultIsolationCompositionTests
{
    [Fact]
    public async Task Failing_startup_stays_on_its_card_and_receiver_still_initializes()
    {
        using var fixture = CompositionFixture.Create(dependencies => dependencies with
        {
            StartupOverride = new FailingStartupService(),
        });

        await fixture.Composition.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, fixture.Runtime.InitializeCalls);
        Assert.Equal("TEST_STARTUP", fixture.Composition.Shell.Settings.Startup.ErrorCode);
        AssertOtherSettingsHealthy(fixture.Composition, except: "startup");
    }

    [Fact]
    public async Task Failing_appearance_stays_on_theme_card_and_receiver_still_initializes()
    {
        using var fixture = CompositionFixture.Create(dependencies => dependencies with
        {
            AppearanceOverride = new FailingAppearanceService(),
        });
        await fixture.Composition.InitializeAsync(CancellationToken.None);

        var paper = fixture.Composition.Shell.Settings.Theme.Options.Single(option => option.Preset == ThemePreset.PaperDay);
        await fixture.Composition.Shell.Settings.Theme.SelectCommand.ExecuteAsync(paper);

        Assert.Equal(1, fixture.Runtime.InitializeCalls);
        Assert.Equal("TEST_APPEARANCE", fixture.Composition.Shell.Settings.Theme.ErrorCode);
        AssertOtherSettingsHealthy(fixture.Composition, except: "theme");
    }

    [Fact]
    public async Task Failing_markdown_stays_in_about_and_receiver_still_initializes()
    {
        using var fixture = CompositionFixture.Create(dependencies => dependencies with
        {
            MarkdownOverride = new FailingMarkdownProvider(),
        });

        await fixture.Composition.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, fixture.Runtime.InitializeCalls);
        Assert.Equal("TEST_MARKDOWN", fixture.Composition.Shell.About.Stories.SelectedItem?.Document.ErrorCode);
        Assert.Equal("TEST_MARKDOWN", fixture.Composition.Shell.About.ReleaseNotes.ErrorCode);
        AssertOtherSettingsHealthy(fixture.Composition, except: null);
    }

    [Fact]
    public async Task Failing_log_export_stays_on_log_card_and_receiver_still_initializes()
    {
        using var fixture = CompositionFixture.Create(dependencies => dependencies with
        {
            LogExportOverride = new FailingLogExportService(),
        });
        await fixture.Composition.InitializeAsync(CancellationToken.None);

        await fixture.Composition.Shell.Settings.LogExport.ExportCommand.ExecuteAsync();

        Assert.Equal(1, fixture.Runtime.InitializeCalls);
        Assert.Equal("TEST_LOG", fixture.Composition.Shell.Settings.LogExport.ErrorCode);
        AssertOtherSettingsHealthy(fixture.Composition, except: "log");
    }

    [Fact]
    public async Task Failing_plugin_catalog_stays_on_plugin_card_and_receiver_still_initializes()
    {
        using var fixture = CompositionFixture.Create(dependencies => dependencies with
        {
            PluginCatalogOverride = new FailingPluginCatalogService(),
        });
        await fixture.Composition.InitializeAsync(CancellationToken.None);

        await fixture.Composition.Shell.Settings.PluginManager.ImportCommand.ExecuteAsync();

        Assert.Equal(1, fixture.Runtime.InitializeCalls);
        Assert.Equal("TEST_PLUGIN", fixture.Composition.Shell.Settings.PluginManager.ErrorCode);
        AssertOtherSettingsHealthy(fixture.Composition, except: "plugin");
    }

    [Fact]
    public async Task Receiver_failure_does_not_block_settings_about_or_packaged_content()
    {
        using var fixture = CompositionFixture.Create();
        fixture.Runtime.InitializeAction = () => Task.FromException(new InvalidOperationException("receiver failed"));

        var result = await fixture.Composition.InitializeAsync(CancellationToken.None);
        fixture.Composition.Shell.Navigation.NavigateCommand.Execute(AppPage.Settings);
        Assert.Same(fixture.Composition.Shell.Settings, fixture.Composition.Shell.CurrentPageViewModel);
        fixture.Composition.Shell.Navigation.NavigateCommand.Execute(AppPage.About);

        Assert.False(result.IsSuccess);
        Assert.Equal("RECEIVER_INITIALIZE", result.ErrorCode);
        Assert.Same(fixture.Composition.Shell.About, fixture.Composition.Shell.CurrentPageViewModel);
        Assert.True(fixture.Composition.Shell.About.Stories.SelectedItem?.Document.IsLoaded);
    }

    private static void AssertOtherSettingsHealthy(ProductionComposition composition, string? except)
    {
        if (except != "startup") Assert.Null(composition.Shell.Settings.Startup.ErrorCode);
        if (except != "theme") Assert.Null(composition.Shell.Settings.Theme.ErrorCode);
        if (except != "plugin") Assert.Null(composition.Shell.Settings.PluginManager.ErrorCode);
        if (except != "log") Assert.Null(composition.Shell.Settings.LogExport.ErrorCode);
    }

    private sealed class CompositionFixture : IDisposable
    {
        private readonly TempDirectory _temp;

        private CompositionFixture(TempDirectory temp, TestReceiverRuntime runtime, ProductionComposition composition) =>
            (_temp, Runtime, Composition) = (temp, runtime, composition);

        public TestReceiverRuntime Runtime { get; }
        public ProductionComposition Composition { get; }

        public static CompositionFixture Create(
            Func<TestProductionDependencies, TestProductionDependencies>? configure = null)
        {
            var temp = TempDirectory.Create();
            var runtime = new TestReceiverRuntime();
            var dependencies = new TestProductionDependencies(
                runtime,
                new FixedLocalAddressResolver("192.168.1.20"),
                new MemoryRegistryStore(),
                temp.Path,
                new RecordingImageSelectionService(),
                new RecordingExternalNavigationService(),
                new RecordingClipboardService(),
                TimeProvider.System);
            dependencies = configure?.Invoke(dependencies) ?? dependencies;
            return new CompositionFixture(temp, runtime, ProductionComposition.CreateForTest(dependencies));
        }

        public void Dispose()
        {
            Composition.Dispose();
            _temp.Dispose();
        }
    }

    private sealed class FailingStartupService : IStartupService
    {
        public StartupSnapshot Snapshot { get; } = new(false, false, "TEST_STARTUP", "startup failed");
        public event Action<StartupSnapshot>? SnapshotChanged { add { } remove { } }
        public Task<OperationResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Failure("TEST_STARTUP", "startup failed"));
    }

    private sealed class FailingAppearanceService : IAppearanceService
    {
        public AppearanceSnapshot Snapshot => AppearanceSnapshot.Default;
        public event Action<AppearanceSnapshot>? SnapshotChanged { add { } remove { } }
        public Task<OperationResult> SelectPresetAsync(ThemePreset preset, CancellationToken cancellationToken) => Fail();
        public Task<OperationResult> SaveCustomPaletteAsync(CustomPalette palette, CancellationToken cancellationToken) => Fail();
        public Task<OperationResult> ImportBackgroundAsync(SelectedImage image, CancellationToken cancellationToken) => Fail();
        public Task<OperationResult> SetReduceMotionAsync(bool reduceMotion, CancellationToken cancellationToken) => Fail();
        public Task<OperationResult> SetFeatureRailWidthAsync(double width, CancellationToken cancellationToken) => Fail();
        private static Task<OperationResult> Fail() =>
            Task.FromResult(OperationResult.Failure("TEST_APPEARANCE", "appearance failed"));
    }

    private sealed class FailingMarkdownProvider : IMarkdownContentProvider
    {
        public Task<OperationResult<string>> GetAsync(MarkdownContentKey key, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<string>.Failure("TEST_MARKDOWN", "markdown failed"));
    }

    private sealed class FailingLogExportService : ILogExportService
    {
        public Task<OperationResult<LogExportReceipt>> ExportAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<LogExportReceipt>.Failure("TEST_LOG", "log failed"));
    }

    private sealed class FailingPluginCatalogService : IPluginCatalogService
    {
        public PluginCatalogSnapshot Snapshot { get; } = new([], true, "test failure");
        public event Action<PluginCatalogSnapshot>? SnapshotChanged { add { } remove { } }
        public Task<OperationResult> ImportAsync(CancellationToken cancellationToken) => Fail();
        public Task<OperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken) => Fail();
        public Task<OperationResult> UninstallAsync(string id, CancellationToken cancellationToken) => Fail();
        private static Task<OperationResult> Fail() =>
            Task.FromResult(OperationResult.Failure("TEST_PLUGIN", "plugin failed"));
    }
}
