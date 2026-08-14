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
using Avalonia;
using MoDi.Core.Infrastructure;
using MoDi.Desktop.Platform.Logging;

namespace MoDi.Desktop;

/// <summary>程序入口 — Avalonia 桌面应用</summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var writer = new StructuredLogService(ApplicationDataPaths.CreateDefault());
        Log.SetImpl(new CoreLoggerAdapter(writer));
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
