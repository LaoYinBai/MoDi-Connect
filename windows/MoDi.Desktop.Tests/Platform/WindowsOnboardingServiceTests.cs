using MoDi.App.Contracts;
using MoDi.Desktop.Platform.Onboarding;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class WindowsOnboardingServiceTests
{
    [Fact]
    public async Task First_launch_is_incomplete_and_skip_persists()
    {
        using var temp = TempDirectory.Create();
        var first = await WindowsOnboardingService.CreateAsync(
            new ApplicationDataPaths(temp.Path), TimeProvider.System, [], CancellationToken.None);
        Assert.False(first.Snapshot.IsCompleted);

        Assert.True((await first.SkipAsync(CancellationToken.None)).IsSuccess);
        var reloaded = await WindowsOnboardingService.CreateAsync(
            new ApplicationDataPaths(temp.Path), TimeProvider.System, [], CancellationToken.None);

        Assert.True(reloaded.Snapshot.IsCompleted);
    }

    [Fact]
    public async Task Diagnostics_isolate_probe_failures()
    {
        using var temp = TempDirectory.Create();
        IOnboardingProbe[] probes =
        [
            new DelegateOnboardingProbe("VB_CABLE", _ => Task.FromResult(new DiagnosticResult("VB_CABLE", true, "ok"))),
            new DelegateOnboardingProbe("USB", _ => throw new IOException("probe failed")),
        ];
        var service = await WindowsOnboardingService.CreateAsync(
            new ApplicationDataPaths(temp.Path), TimeProvider.System, probes, CancellationToken.None);

        var result = await service.RunDiagnosticsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Collection(service.Snapshot.Diagnostics,
            item => Assert.True(item.IsSuccess),
            item => Assert.Equal("USB", item.Key));
        Assert.False(service.Snapshot.Diagnostics[1].IsSuccess);
    }

    [Theory]
    [InlineData(false, false, "未检测到蓝牙硬件")]
    [InlineData(true, false, "蓝牙硬件已检测到，但当前未启用")]
    [InlineData(true, true, "蓝牙硬件已检测到且已启用")]
    public void Bluetooth_diagnostic_reports_presence_and_enabled_state(
        bool hardwarePresent,
        bool enabled,
        string expectedMessage)
    {
        var result = WindowsOnboardingService.BuildBluetoothDiagnostic(hardwarePresent, enabled);

        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(hardwarePresent && enabled, result.IsSuccess);
    }

    [Theory]
    [InlineData(false, "", "应用内置 ADB 不可用")]
    [InlineData(true, "List of devices attached\r\n", "USB 功能可用，暂未检测到已授权设备")]
    [InlineData(true, "List of devices attached\r\nABC\tunauthorized\r\n", "已检测到 USB 设备，请在手机上允许 USB 调试")]
    [InlineData(true, "List of devices attached\r\nABC\tdevice\r\n", "USB 功能已启用，设备已授权")]
    public void Usb_diagnostic_reports_runtime_hardware_and_authorization(
        bool packagedAdbPresent,
        string adbOutput,
        string expectedMessage)
    {
        var result = WindowsOnboardingService.BuildUsbDiagnostic(packagedAdbPresent, adbOutput);

        Assert.Equal(expectedMessage, result.Message);
    }
}
