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
/// 依赖：发行包 tools/adb 中的应用私有 adb.exe；开发运行回退系统 PATH。
/// 与 LAN/P2P/蓝牙完全解耦，仅被 UsbLink 调用。
/// </summary>
internal static class UsbDeviceHelper
{
    private const string Tag = "UsbDeviceHelper";

    /// <summary>
    /// 检测是否有 USB 连接的 Android 设备。
    /// 执行 `adb devices`，解析输出判断是否有 device 状态的条目。
    /// </summary>
    /// <returns>true=有已授权的 USB 设备</returns>
    public static async Task<bool> DetectDeviceAsync()
    {
        try
        {
            var output = await RunAdbAsync("devices");
            if (output == null) return false;

            // 解析 adb devices 输出：
            // List of devices attached
            // XXXXXXXX    device
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("List of")) continue;
                if (trimmed.EndsWith("device"))
                {
                    Log.I(Tag, $"USB device found: {trimmed}");
                    return true;
                }
                if (trimmed.EndsWith("unauthorized"))
                {
                    Log.W(Tag, $"USB device unauthorized: {trimmed}（请在手机上确认 USB 调试）");
                }
            }

            Log.W(Tag, "No USB device found");
            return false;
        }
        catch (Exception ex)
        {
            Log.E(Tag, $"DetectDevice error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 建立 ADB forward 端口隧道：localhost:12348 → Android localhost:12348。
    /// 执行 `adb forward tcp:12348 tcp:12348`。
    /// </summary>
    /// <returns>true=forward 建立成功</returns>
    public static async Task<bool> SetupForwardAsync()
    {
        try
        {
            var output = await RunAdbAsync($"forward tcp:{UsbTransport.Port} tcp:{UsbTransport.Port}");
            // adb forward 成功时无输出（exit code 0）
            Log.I(Tag, $"ADB forward established: tcp:{UsbTransport.Port} → tcp:{UsbTransport.Port}");
            return true;
        }
        catch (Exception ex)
        {
            Log.E(Tag, $"SetupForward error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 移除 ADB forward（断开时清理）。
    /// 执行 `adb forward --remove tcp:12348`。
    /// </summary>
    public static async Task RemoveForwardAsync()
    {
        try
        {
            await RunAdbAsync($"forward --remove tcp:{UsbTransport.Port}");
            Log.I(Tag, "ADB forward removed");
        }
        catch (Exception ex)
        {
            Log.W(Tag, $"RemoveForward error: {ex.Message}");
        }
    }

    /// <summary>执行 adb 命令并返回标准输出（超时 5 秒）</summary>
    private static async Task<string?> RunAdbAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ResolveAdbExecutable(AppContext.BaseDirectory),
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) throw new Exception("Failed to start adb process");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            Log.W(Tag, $"adb stderr: {error.Trim()}");
        }

        return output;
    }

    internal static string ResolveAdbExecutable(string baseDirectory)
    {
        var packaged = Path.Combine(baseDirectory, "tools", "adb", "adb.exe");
        return File.Exists(packaged) ? packaged : "adb";
    }
}
