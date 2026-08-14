using System;
using MoDi.App.Contracts;
using MoDi.Desktop.Adapters;
using MoDi.Desktop.Platform.Startup;

namespace MoDi.Desktop.Composition;

internal sealed record TestProductionDependencies(
    IReceiverRuntime ReceiverRuntime,
    ILocalAddressResolver LocalAddressResolver,
    IRegistryStore RegistryStore,
    string ApplicationDataRoot,
    IImageSelectionService ImageSelection,
    IExternalNavigationService ExternalNavigation,
    IClipboardService Clipboard,
    TimeProvider TimeProvider)
{
    public IStartupService? StartupOverride { get; init; }
    public IAppearanceService? AppearanceOverride { get; init; }
    public IMarkdownContentProvider? MarkdownOverride { get; init; }
    public ILogExportService? LogExportOverride { get; init; }
    public IPluginCatalogService? PluginCatalogOverride { get; init; }
}
