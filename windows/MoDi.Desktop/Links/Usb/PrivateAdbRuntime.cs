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

/// <summary>One foreground server per application instance; never sends kill-server to any endpoint.</summary>
internal sealed class PrivateAdbRuntime(string applicationRoot, string stateRoot) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Process? _server;
    private OwnedProcessJob? _job;
    private int _port;
    private bool _disposed;

    internal static ProcessStartInfo CreateStartInfo(string applicationRoot, string stateRoot, int port, string[] arguments)
    {
        if (port is <= 0 or > 65535 || port == 5037) throw new ArgumentOutOfRangeException(nameof(port));
        var executable = UsbDeviceHelper.ResolveAdbExecutable(applicationRoot);
        var bin = Path.GetDirectoryName(executable)!;
        foreach (var dll in new[] { "AdbWinApi.dll", "AdbWinUsbApi.dll" }) PrivateToolEnvironment.RequireFile(Path.Combine(bin, dll));
        var start = new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = bin
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
            if (_server is null || _server.HasExited)
            {
                if (!startIfNeeded) return string.Empty;
                await StartServerAsync(timeout.Token).ConfigureAwait(false);
            }
            using var process = new Process { StartInfo = CreateStartInfo(applicationRoot, stateRoot, _port, arguments) };
            process.Start();
            try
            {
                _job!.Assign(process);
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

    private async Task StartServerAsync(CancellationToken token)
    {
        StopServer();
        // Ask Windows for an available loopback port, not the shared ADB port. If binding loses a race,
        // the foreground server exits and this operation fails; we never attach to the occupying server.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        _job = new OwnedProcessJob();
        await EnsurePrivateKeyAsync(token).ConfigureAwait(false);
        _server = new Process { StartInfo = CreateStartInfo(applicationRoot, stateRoot, _port, ["server", "nodaemon"]) };
        _server.Start();
        try
        {
            _job.Assign(_server);
            _server.OutputDataReceived += (_, _) => { };
            _server.ErrorDataReceived += (_, _) => { }; // Drain, do not log authorization/key material.
            _server.BeginOutputReadLine();
            _server.BeginErrorReadLine();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (_server.HasExited) throw new IOException("内置 ADB 服务端启动失败；未连接系统服务端。");
                using var client = new TcpClient();
                try
                {
                    await client.ConnectAsync(IPAddress.Loopback, _port, token).ConfigureAwait(false);
                    await Task.Delay(75, token).ConfigureAwait(false);
                    if (_server.HasExited) throw new IOException("内置 ADB 端口不可用；未连接已有服务端。");
                    break;
                }
                catch (SocketException) { await Task.Delay(50, token).ConfigureAwait(false); }
            }
            Log.I("PrivateAdb", $"应用私有 ADB 就绪：PID={_server.Id}，端口={_port}，路径={_server.StartInfo.FileName}");
        }
        catch { StopServer(); throw; }
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
            _job!.Assign(process);
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
    private void StopServer()
    {
        _job?.Dispose(); _job = null;
        if (_server is not null) { StopOwned(_server); _server.Dispose(); _server = null; }
    }
    public void Dispose()
    {
        _lifetime.Cancel();
        _gate.Wait();
        try { if (_disposed) return; _disposed = true; StopServer(); }
        finally { _gate.Release(); }
    }
}
