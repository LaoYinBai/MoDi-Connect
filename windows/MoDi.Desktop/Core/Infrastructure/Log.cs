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

namespace MoDi.Core.Infrastructure;

/// <summary>
/// Log — ILogger 静态门面
/// 全局唯一日志入口，所有模块通过此类输出日志。
/// 启动时调用 Log.SetImpl() 注入具体实现。
/// </summary>
public static class Log
{
    private static ILogger _impl = new ConsoleLogger();

    public static void SetImpl(ILogger impl) => _impl = impl ?? throw new ArgumentNullException(nameof(impl));

    public static void D(string tag, string msg) => _impl.Debug(tag, msg);
    public static void I(string tag, string msg) => _impl.Info(tag, msg);
    public static void W(string tag, string msg) => _impl.Warn(tag, msg);
    public static void E(string tag, string msg) => _impl.Error(tag, msg);
    public static void E(string tag, string msg, Exception ex) => _impl.Error(tag, msg, ex);
}
