using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Features;

public sealed class BuiltInFeatureCatalogService : IPluginCatalogService
{
    private readonly IReadOnlyList<IBuiltInFeature> _features;
    private readonly Dictionary<string, bool> _enabled;
    private readonly string _version;
    private readonly SynchronizationContext? _uiContext;

    public BuiltInFeatureCatalogService(IEnumerable<IBuiltInFeature> features, string? version = null)
    {
        _features = (features ?? throw new ArgumentNullException(nameof(features))).ToArray();
        if (_features.Select(feature => feature.Descriptor.Id).Distinct(StringComparer.Ordinal).Count() != _features.Count)
            throw new ArgumentException("内置功能 ID 必须唯一", nameof(features));
        _enabled = _features.ToDictionary(feature => feature.Descriptor.Id, _ => true, StringComparer.Ordinal);
        _version = string.IsNullOrWhiteSpace(version)
            ? typeof(BuiltInFeatureCatalogService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"
            : version;
        _uiContext = SynchronizationContext.Current;
        Snapshot = BuildSnapshot();
    }

    public PluginCatalogSnapshot Snapshot { get; private set; }
    public event Action<PluginCatalogSnapshot>? SnapshotChanged;

    public Task<OperationResult> ImportAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult.Failure(
            "PLUGIN_HOST_UNAVAILABLE",
            "当前版本仅开放内置功能，外部插件主机尚未启用"));
    }

    public Task<OperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_enabled.ContainsKey(id))
            return Task.FromResult(OperationResult.Failure("PLUGIN_NOT_FOUND", "找不到指定插件"));
        _enabled[id] = enabled;
        Publish();
        return Task.FromResult(OperationResult.Success());
    }

    public Task<OperationResult> UninstallAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_enabled.ContainsKey(id))
            return Task.FromResult(OperationResult.Failure(
                "PLUGIN_BUILTIN_PROTECTED",
                "内置功能不可卸载"));
        return Task.FromResult(OperationResult.Failure("PLUGIN_NOT_FOUND", "找不到指定插件"));
    }

    private PluginCatalogSnapshot BuildSnapshot() => new(
        _features.Select(feature =>
        {
            var enabled = _enabled[feature.Descriptor.Id];
            return new PluginEntrySnapshot(
                feature.Descriptor.Id,
                feature.Descriptor.DisplayName,
                IsBuiltIn: true,
                IsEnabled: enabled,
                CanUninstall: false,
                enabled ? PluginHealth.BuiltIn : PluginHealth.Disabled,
                enabled ? "内置功能 · 运行正常" : "内置功能 · 已停用",
                new PluginDeveloperMetadata(
                    _version,
                    "Silvite",
                    feature.Descriptor.Id == "audio" ? ["audio.receive"] : []));
        }).ToArray(),
        CanImportExternal: false,
        "V1：内置功能；外部插件主机将在后续版本开放");

    private void Publish()
    {
        Snapshot = BuildSnapshot();
        var snapshot = Snapshot;
        var handler = SnapshotChanged;
        if (handler is null)
            return;
        if (_uiContext is null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
            handler(snapshot);
        else
            _uiContext.Post(_ => handler(snapshot), null);
    }
}
