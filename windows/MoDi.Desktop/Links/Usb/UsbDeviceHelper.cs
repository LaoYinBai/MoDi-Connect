/*
 * MoDi Connect - Cross-device interconnection protocol
 * Copyright (C) 2026 Silvite
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using MoDi.Desktop.Platform.Runtime;
using MoDi.Core.Adapters;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop.Links;

/// <summary>
/// UsbDeviceHelper — USB 设备检测与 ADB forward 管理
///
/// 职责：
///   1. 执行 `adb devices` 检测是否有 USB 连接的 Android 设备
///   2. 执行 `adb forward tcp:12348 tcp:12348` 建立端口隧道
///
/// 依赖：仅发行包 tools/adb 中的应用私有 adb.exe，不回退系统 PATH。
/// 与 LAN/P2P/蓝牙完全解耦，仅被 UsbLink 调用。
/// </summary>
internal static class UsbDeviceHelper
{
    private const string Tag = "UsbDeviceHelper";
    private static string? _detectedSerial;
    private static string? _forwardSerial;
    private static readonly AdbOperationLifetime Operations = new();
    private static readonly Lazy<PrivateAdbRuntime> Runtime = new(() => new PrivateAdbRuntime(
        AppContext.BaseDirectory, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoDi", "Runtime", "adb")));

    internal static async Task<string> QueryDevicesAsync(CancellationToken token)
    {
        using var linked = Operations.CreateLinkedSource(token);
        linked.Token.ThrowIfCancellationRequested();
        return await Runtime.Value.RunAsync(["devices"], linked.Token).ConfigureAwait(false);
    }

    internal static void BeginShutdown() => Operations.BeginShutdown();

    internal static void Shutdown()
    {
        Operations.BeginShutdown();
        if (Runtime.IsValueCreated)
            Runtime.Value.Dispose();
    }

    /// <summary>
    /// 检测是否有 USB 连接的 Android 设备。
    /// 执行 `adb devices`，解析输出判断是否有 device 状态的条目。
    /// </summary>
    /// <returns>true=有已授权的 USB 设备</returns>
    public static async Task<bool> DetectDeviceAsync(CancellationToken token = default)
    {
        try
        {
            var output = await QueryDevicesAsync(token);
            if (output == null) return false;

            var serial = SelectUniqueAuthorizedSerial(output);
            Volatile.Write(ref _detectedSerial, null);
            if (serial == null)
            {
                Log.D(Tag, "ADB discovery: no unique authorized target; no forward requested");
                return false;
            }
            // Confirm the candidate is the unique physical USB transport, not an ADB Wi-Fi peer.
            using var linked = Operations.CreateLinkedSource(token);
            var usbSerial = await Runtime.Value.RunAsync(["-d", "get-serialno"], linked.Token).ConfigureAwait(false);
            if (!string.Equals(usbSerial.Trim(), serial, StringComparison.Ordinal)) return false;
            Volatile.Write(ref _detectedSerial, serial);
            Log.D(Tag, "ADB discovery: unique authorized USB target confirmed");
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || Operations.IsShuttingDown)
        {
            Log.D(Tag, "ADB device detection stopped during shutdown");
            return false;
        }
        catch (Exception ex)
        {
            Log.E(Tag, $"DetectDevice error: {ex.Message}");
            return false;
        }
    }

    internal static string? SelectUniqueAuthorizedSerial(string output)
    {
        string? selected = null;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2 || fields[1] != "device") continue;
            if (selected != null) return null;
            selected = fields[0];
        }
        return selected;
    }

    /// <summary>
    /// 建立 ADB forward 端口隧道：localhost:12348 → Android localhost:12348。
    /// 执行 `adb forward tcp:12348 tcp:12348`。
    /// </summary>
    /// <returns>true=forward 建立成功</returns>
    public static async Task<bool> SetupForwardAsync(CancellationToken token = default)
    {
        try
        {
            using var linked = Operations.CreateLinkedSource(token);
            linked.Token.ThrowIfCancellationRequested();
            var serial = Volatile.Read(ref _detectedSerial);
            if (serial == null) return false;
            await ReplaceForwardAsync(Runtime.Value.RunAsync, serial, UsbTransport.Port, linked.Token).ConfigureAwait(false);
            Volatile.Write(ref _forwardSerial, serial);
            // adb forward 成功时无输出（exit code 0）
            Log.I(Tag, $"ADB forward established: tcp:{UsbTransport.Port} → tcp:{UsbTransport.Port}");
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || Operations.IsShuttingDown)
        {
            Log.D(Tag, "ADB forward setup stopped during shutdown");
            return false;
        }
        catch (Exception ex)
        {
            Log.E(Tag, $"SetupForward error: {ex.Message}");
            return false;
        }
    }

    internal static async Task ReplaceForwardAsync(
        Func<string[], CancellationToken, bool, Task<string>> runAsync,
        string serial,
        int port,
        CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        var endpoint = $"tcp:{port}";
        try
        {
            await runAsync(["-s", serial, "forward", "--remove", endpoint], token, false).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // A missing listener is the expected first-connect state. The create command below
            // remains authoritative and will surface real device/server failures.
        }

        await runAsync(["-s", serial, "forward", endpoint, endpoint], token, true).ConfigureAwait(false);
    }

    /// <summary>
    /// 移除 ADB forward（断开时清理）。
    /// 执行 `adb forward --remove tcp:12348`。
    /// </summary>
    public static async Task RemoveForwardAsync()
    {
        try
        {
            if (!Runtime.IsValueCreated) return;
            var serial = Interlocked.Exchange(ref _forwardSerial, null);
            if (serial == null) return;
            await Runtime.Value.RunAsync(["-s", serial, "forward", "--remove", $"tcp:{UsbTransport.Port}"], CancellationToken.None, startIfNeeded: false);
            Log.I(Tag, "ADB forward removed");
        }
        catch (IOException ex) when (IsMissingForwardError(ex))
        {
            Log.D(Tag, "ADB forward was already absent during cleanup");
        }
        catch (Exception ex)
        {
            Log.W(Tag, $"RemoveForward error: {ex.Message}");
        }
    }

    internal static bool IsMissingForwardError(IOException exception) =>
        exception.Message.Contains("listener", StringComparison.OrdinalIgnoreCase)
        && exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);

    internal static string ResolveAdbExecutable(string baseDirectory)
    {
        return PrivateToolEnvironment.RequireFile(Path.Combine(Path.GetFullPath(baseDirectory), "tools", "adb", "adb.exe"));
    }
}
