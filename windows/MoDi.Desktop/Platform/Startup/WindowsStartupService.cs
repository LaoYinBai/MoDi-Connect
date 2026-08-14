using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Startup;

internal interface IRegistryStore
{
    string? ReadCurrentUserString(string subKey, string valueName);
    void WriteCurrentUserString(string subKey, string valueName, string value);
    void DeleteCurrentUserValue(string subKey, string valueName);
}

internal sealed class RegistryStore : IRegistryStore
{
    public string? ReadCurrentUserString(string subKey, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: false);
        return key?.GetValue(valueName) as string;
    }

    public void WriteCurrentUserString(string subKey, string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException("无法打开当前用户启动项");
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    public void DeleteCurrentUserValue(string subKey, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}

public sealed class WindowsStartupService : IStartupService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MoDi";
    private readonly IRegistryStore _registry;
    private readonly string _command;
    private readonly SynchronizationContext? _uiContext;

    public WindowsStartupService(string executablePath)
        : this(new RegistryStore(), executablePath) { }

    internal WindowsStartupService(IRegistryStore registry, string executablePath)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException("可执行文件路径不能为空", nameof(executablePath));
        _command = $"\"{executablePath}\" --background";
        _uiContext = SynchronizationContext.Current;
        Snapshot = ReadSnapshot();
    }

    public StartupSnapshot Snapshot { get; private set; }
    public event Action<StartupSnapshot>? SnapshotChanged;

    public Task<OperationResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (enabled)
                _registry.WriteCurrentUserString(RunSubKey, ValueName, _command);
            else
                _registry.DeleteCurrentUserValue(RunSubKey, ValueName);
            Publish(new StartupSnapshot(enabled, true, null, null));
            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception ex)
        {
            const string code = "STARTUP_REGISTRY_ACCESS";
            var message = $"无法修改当前用户开机启动：{ex.Message}";
            Publish(new StartupSnapshot(false, false, code, message));
            return Task.FromResult(OperationResult.Failure(code, message));
        }
    }

    private StartupSnapshot ReadSnapshot()
    {
        try
        {
            var value = _registry.ReadCurrentUserString(RunSubKey, ValueName);
            return new StartupSnapshot(
                string.Equals(value, _command, StringComparison.OrdinalIgnoreCase),
                true,
                null,
                null);
        }
        catch (Exception ex)
        {
            return new StartupSnapshot(
                false,
                false,
                "STARTUP_REGISTRY_ACCESS",
                $"无法读取当前用户开机启动：{ex.Message}");
        }
    }

    private void Publish(StartupSnapshot snapshot)
    {
        Snapshot = snapshot;
        var handler = SnapshotChanged;
        if (handler is null)
            return;
        if (_uiContext is null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
            handler(snapshot);
        else
            _uiContext.Post(_ => handler(snapshot), null);
    }
}
