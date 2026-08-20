using System;
using System.Collections.Generic;
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
        BooleanProbe("BLUETOOTH", "检查 Windows 蓝牙适配器", () => BluetoothRadio.Default is not null),
        BooleanProbe("USB", "检查 PATH 中的 adb.exe", HasAdb),
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

    private static bool HasAdb()
    {
        var executable = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(directory => File.Exists(Path.Combine(directory, executable)));
    }

    private sealed record OnboardingStateV1(int SchemaVersion, bool IsCompleted);
}
