using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MoDi.Core.Infrastructure;
using MoDi.Desktop.Platform.Runtime;

namespace MoDi.Desktop.Links;

/// <summary>
/// Runs the bundled ADB client against the standard local ADB server endpoint.
/// ADB permits only one server to own a USB device, so a second private server cannot coexist
/// with Android Studio or other normal ADB clients. This runtime never resolves adb from PATH
/// and never sends kill-server to the shared endpoint.
/// </summary>
internal sealed class PrivateAdbRuntime(string applicationRoot, string stateRoot) : IDisposable
{
    private const int StandardServerPort = 5037;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    internal static ProcessStartInfo CreateStartInfo(string applicationRoot, string stateRoot, int port, string[] arguments)
    {
        if (port is <= 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var executable = UsbDeviceHelper.ResolveAdbExecutable(applicationRoot);
        var bin = Path.GetDirectoryName(executable)!;
        foreach (var dll in new[] { "AdbWinApi.dll", "AdbWinUsbApi.dll" }) PrivateToolEnvironment.RequireFile(Path.Combine(bin, dll));
        var start = new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = bin
        };
        PrivateToolEnvironment.Apply(start, stateRoot, bin);
        start.Environment["ADB_VENDOR_KEYS"] = PrivateKeyPath(stateRoot);
        // Explicit CLI options and a clean environment prevent ADB_SERVER_SOCKET/ANDROID_SERIAL overrides.
        start.ArgumentList.Add("-L");
        // Windows ADB does not support a hostname in its listener specification. tcp:PORT
        // binds loopback by default (never pass -a); the client uses the same local endpoint.
        start.ArgumentList.Add("tcp:" + port.ToString(CultureInfo.InvariantCulture));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }

    public async Task<string> RunAsync(string[] arguments, CancellationToken token, bool startIfNeeded = true)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        await _gate.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!startIfNeeded && !await IsServerAvailableAsync(timeout.Token).ConfigureAwait(false))
                return string.Empty;

            await EnsurePrivateKeyAsync(timeout.Token).ConfigureAwait(false);
            using var process = new Process
            {
                StartInfo = CreateStartInfo(applicationRoot, stateRoot, StandardServerPort, arguments)
            };
            process.Start();
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var error = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                var text = await output.ConfigureAwait(false);
                var errorText = await error.ConfigureAwait(false);
                if (process.ExitCode != 0) throw new IOException($"内置 ADB 命令失败（{process.ExitCode}）：{errorText.Trim()}");
                return text;
            }
            finally { StopOwned(process); }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && !_lifetime.IsCancellationRequested)
        { throw new TimeoutException("内置 ADB 响应超时；不会回退或重启系统 ADB。"); }
        finally { _gate.Release(); }
    }

    private static async Task<bool> IsServerAvailableAsync(CancellationToken token)
    {
        using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        probeTimeout.CancelAfter(TimeSpan.FromMilliseconds(250));
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, StandardServerPort, probeTimeout.Token)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
        catch (SocketException) { return false; }
    }

    private async Task EnsurePrivateKeyAsync(CancellationToken token)
    {
        var key = PrivateKeyPath(stateRoot);
        var publicKey = key + ".pub";
        if (File.Exists(key) && File.Exists(publicKey)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(key)!);
        // An interrupted first launch must not leave a half-key that ADB later trusts.
        if (File.Exists(key)) File.Delete(key);
        if (File.Exists(publicKey)) File.Delete(publicKey);
        var executable = UsbDeviceHelper.ResolveAdbExecutable(applicationRoot);
        var bin = Path.GetDirectoryName(executable)!;
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            WorkingDirectory = bin,
        };
        PrivateToolEnvironment.Apply(start, stateRoot, bin);
        start.ArgumentList.Add("keygen");
        start.ArgumentList.Add(key);
        using var process = new Process { StartInfo = start };
        process.Start();
        try
        {
            var error = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(key) || !File.Exists(publicKey))
                throw new IOException($"内置 ADB 私有授权初始化失败（{process.ExitCode}）：{(await error.ConfigureAwait(false)).Trim()}");
        }
        finally { StopOwned(process); }
    }

    private static string PrivateKeyPath(string root) =>
        Path.Combine(Path.GetFullPath(root), "auth", "adbkey");

    private static void StopOwned(Process process)
    {
        try { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(2000); } }
        catch (InvalidOperationException) { }
    }
    public void Dispose()
    {
        _lifetime.Cancel();
        _gate.Wait();
        try { if (_disposed) return; _disposed = true; }
        finally { _gate.Release(); }
    }
}
