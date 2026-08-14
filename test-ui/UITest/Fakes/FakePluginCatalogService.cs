using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakePluginCatalogService : IPluginCatalogService
{
    public FakePluginCatalogService() => Publish(CreateBuiltInSnapshot());

    public PluginCatalogSnapshot Snapshot { get; private set; } = new([], true, string.Empty);
    public int ImportCalls { get; private set; }
    public int SetEnabledCalls { get; private set; }
    public int UninstallCalls { get; private set; }
    public event Action<PluginCatalogSnapshot>? SnapshotChanged;

    public Task<OperationResult> ImportAsync(CancellationToken cancellationToken)
    {
        ImportCalls++;
        return Task.FromResult(OperationResult.Success("插件导入已完成"));
    }

    public Task<OperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        SetEnabledCalls++;
        Publish(Snapshot with
        {
            Entries = Snapshot.Entries.Select(entry => entry.Id == id
                ? entry with
                {
                    IsEnabled = enabled,
                    Health = enabled ? PluginHealth.Healthy : PluginHealth.Disabled,
                    Detail = enabled ? "运行正常" : "已停用"
                }
                : entry).ToArray()
        });
        return Task.FromResult(OperationResult.Success());
    }

    public Task<OperationResult> UninstallAsync(string id, CancellationToken cancellationToken)
    {
        UninstallCalls++;
        var entry = Snapshot.Entries.FirstOrDefault(candidate => candidate.Id == id);
        if (entry is { IsBuiltIn: true })
            return Task.FromResult(OperationResult.Failure("PLUGIN_BUILT_IN", "内置插件不可卸载"));

        Publish(Snapshot with { Entries = Snapshot.Entries.Where(candidate => candidate.Id != id).ToArray() });
        return Task.FromResult(OperationResult.Success());
    }

    public void SetScenario(string scenario)
    {
        if (string.Equals(scenario, "empty", StringComparison.OrdinalIgnoreCase))
        {
            Publish(new PluginCatalogSnapshot([], true, "暂无插件"));
            return;
        }

        var health = scenario.ToLowerInvariant() switch
        {
            "disabled" => PluginHealth.Disabled,
            "incompatible" => PluginHealth.Incompatible,
            "crashed" => PluginHealth.Crashed,
            "loading" => PluginHealth.Loading,
            _ => PluginHealth.Healthy,
        };
        var enabled = health is not PluginHealth.Disabled;
        Publish(new PluginCatalogSnapshot(
        [
            new PluginEntrySnapshot(
                "demo-external-plugin",
                "演示插件",
                IsBuiltIn: false,
                IsEnabled: enabled,
                CanUninstall: true,
                health,
                health switch
                {
                    PluginHealth.Disabled => "已停用",
                    PluginHealth.Incompatible => "版本不兼容",
                    PluginHealth.Crashed => "独立进程已崩溃",
                    PluginHealth.Loading => "正在加载",
                    _ => "运行正常",
                },
                new PluginDeveloperMetadata("1.0.0", "TestUI", ["demo.visual-state"]))
        ],
        true,
        "V1：.NET DLL + 独立 EXE"));
    }

    public void Publish(PluginCatalogSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    private static PluginCatalogSnapshot CreateBuiltInSnapshot() => new(
    [
        new PluginEntrySnapshot(
            "built-in-audio",
            "音频",
            IsBuiltIn: true,
            IsEnabled: true,
            CanUninstall: false,
            PluginHealth.BuiltIn,
            "内置插件 · 运行正常",
            new PluginDeveloperMetadata("1.0.0", "MoDi", ["audio.receive"]))
    ],
    true,
    "V1：.NET DLL + 独立 EXE");
}
