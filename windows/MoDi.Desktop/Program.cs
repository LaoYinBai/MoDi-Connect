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
using System.Threading;
using Avalonia;
using MoDi.Core.Infrastructure;
using MoDi.Desktop.Diagnostics;
using MoDi.Desktop.Platform.Logging;

namespace MoDi.Desktop;

/// <summary>程序入口 — Avalonia 桌面应用</summary>
internal static class Program
{
    private const string InstanceMutexName = @"Global\MoDi.Connect.Desktop.SingleInstance";

    [STAThread]
    public static void Main(string[] args)
    {
        using var writer = new StructuredLogService(ApplicationDataPaths.CreateDefault());
        Log.SetImpl(new CoreLoggerAdapter(writer));
        using var exceptionBoundary = GlobalExceptionBoundary.Install();

        // 单实例守卫：旧实例（含僵尸进程）存活时占用 12345/12347 端口，
        // 新实例继续启动只会造成"握手/音频分属两个进程"的假象，直接退出并留日志。
        using var instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            Log.W("Program", "检测到已有 MoDi 实例正在运行（可能占用音频/握手端口），本次启动退出");
            return;
        }

        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        finally { Links.UsbDeviceHelper.Shutdown(); }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
