using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;
using MoDi.Desktop.Platform.Storage;
using InTheHand.Net.Bluetooth;
using NAudio.CoreAudioApi;

namespace MoDi.Desktop.Platform.Onboarding;

internal interface IOnboardingProbe
{
    string Key { get; }
    Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken);
}

internal sealed class DelegateOnboardingProbe(
    string key,
    Func<CancellationToken, Task<DiagnosticResult>> run) : IOnboardingProbe
{
    public string Key { get; } = key;
    public Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken) => run(cancellationToken);
}

public sealed class WindowsOnboardingService : IOnboardingService
{
    private readonly AtomicJsonStore<OnboardingStateV1> _store;
    private readonly IReadOnlyList<IOnboardingProbe> _probes;

    private WindowsOnboardingService(
        AtomicJsonStore<OnboardingStateV1> store,
        IReadOnlyList<IOnboardingProbe> probes,
        OnboardingSnapshot snapshot) =>
        (_store, _probes, Snapshot) = (store, probes, snapshot);

    public OnboardingSnapshot Snapshot { get; private set; }
    public event Action<OnboardingSnapshot>? SnapshotChanged;

    internal static async Task<WindowsOnboardingService> CreateAsync(
        ApplicationDataPaths paths,
        TimeProvider timeProvider,
        IReadOnlyList<IOnboardingProbe>? probes,
        CancellationToken cancellationToken)
    {
        var store = new AtomicJsonStore<OnboardingStateV1>(
            paths.OnboardingSettingsFile,
            timeProvider,
            new JsonSerializerOptions { WriteIndented = true });
        var state = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = state is { SchemaVersion: 1 }
            ? new OnboardingSnapshot(state.IsCompleted, 0, [])
            : OnboardingSnapshot.Default;
        return new WindowsOnboardingService(store, probes ?? CreateDefaultProbes(), snapshot);
    }

    internal static Task<WindowsOnboardingService> CreateAsync(
        ApplicationDataPaths paths,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CreateAsync(paths, timeProvider, null, cancellationToken);

    public async Task<OperationResult> RunDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var results = new List<DiagnosticResult>(_probes.Count);
        foreach (var probe in _probes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                results.Add(await probe.RunAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                results.Add(new DiagnosticResult(probe.Key, false, exception.Message));
            }
        }

        Publish(Snapshot with { Diagnostics = results });
        return OperationResult.Success();
    }

    public Task<OperationResult> CompleteAsync(CancellationToken cancellationToken) =>
        PersistCompletionAsync(cancellationToken);

    public Task<OperationResult> SkipAsync(CancellationToken cancellationToken) =>
        PersistCompletionAsync(cancellationToken);

    private async Task<OperationResult> PersistCompletionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _store.WriteAsync(new OnboardingStateV1(1, true), cancellationToken).ConfigureAwait(false);
            Publish(Snapshot with { IsCompleted = true });
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return OperationResult.Failure("ONBOARDING_STORAGE", $"保存引导状态失败：{exception.Message}");
        }
    }

    private void Publish(OnboardingSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    private static IReadOnlyList<IOnboardingProbe> CreateDefaultProbes() =>
    [
        BooleanProbe("VB_CABLE", "检查活动音频设备中的 VB-CABLE", HasVbCable),
        BooleanProbe("FIREWALL_AUDIO", "检查 UDP 12345 是否已监听", () => IsUdpPortListening(TransportIdentity.AudioPort)),
        BooleanProbe("FIREWALL_HANDSHAKE", "检查 UDP 12347 是否已监听", () => IsUdpPortListening(TransportIdentity.HandshakePort)),
        BooleanProbe("NETWORK_ADDRESS", "已检测到活动网络接口", () =>
            NetworkInterface.GetAllNetworkInterfaces().Any(item =>
                item.OperationalStatus == OperationalStatus.Up &&
                item.NetworkInterfaceType != NetworkInterfaceType.Loopback)),
        new DelegateOnboardingProbe("BLUETOOTH", _ => Task.FromResult(ProbeBluetooth())),
        new DelegateOnboardingProbe("USB", ProbeUsbAsync),
    ];

    private static IOnboardingProbe BooleanProbe(string key, string message, Func<bool> probe) =>
        new DelegateOnboardingProbe(key, _ => Task.FromResult(
            new DiagnosticResult(key, probe(), message)));

    private static bool HasVbCable()
    {
        using var devices = new MMDeviceEnumerator();
        return devices.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
            .Any(device => device.FriendlyName.Contains("CABLE", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUdpPortListening(int port) =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
            .Any(endpoint => endpoint.Port == port);

    private static DiagnosticResult ProbeBluetooth()
    {
        var radio = BluetoothRadio.Default;
        return BuildBluetoothDiagnostic(
            hardwarePresent: radio is not null,
            enabled: radio?.Mode is RadioMode.Connectable or RadioMode.Discoverable);
    }

    internal static DiagnosticResult BuildBluetoothDiagnostic(bool hardwarePresent, bool enabled) =>
        !hardwarePresent
            ? new("BLUETOOTH", false, "未检测到蓝牙硬件")
            : enabled
                ? new("BLUETOOTH", true, "蓝牙硬件已检测到且已启用")
                : new("BLUETOOTH", false, "蓝牙硬件已检测到，但当前未启用");

    private static async Task<DiagnosticResult> ProbeUsbAsync(CancellationToken cancellationToken)
    {
        var adb = Path.Combine(AppContext.BaseDirectory, "tools", "adb", "adb.exe");
        if (!File.Exists(adb))
            return BuildUsbDiagnostic(false, string.Empty);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            var start = new ProcessStartInfo(adb)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("devices");
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("无法启动应用内置 ADB");
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return BuildUsbDiagnostic(true, await outputTask.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DiagnosticResult("USB", false, "USB 检测超时，可继续使用 LAN 或万能链路");
        }
    }

    internal static DiagnosticResult BuildUsbDiagnostic(bool packagedAdbPresent, string adbOutput)
    {
        if (!packagedAdbPresent)
            return new("USB", false, "应用内置 ADB 不可用");
        var lines = adbOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Any(line => line.EndsWith("\tdevice", StringComparison.OrdinalIgnoreCase)))
            return new("USB", true, "USB 功能已启用，设备已授权");
        if (lines.Any(line => line.EndsWith("\tunauthorized", StringComparison.OrdinalIgnoreCase)))
            return new("USB", false, "已检测到 USB 设备，请在手机上允许 USB 调试");
        return new("USB", true, "USB 功能可用，暂未检测到已授权设备");
    }

    private sealed record OnboardingStateV1(int SchemaVersion, bool IsCompleted);
}
