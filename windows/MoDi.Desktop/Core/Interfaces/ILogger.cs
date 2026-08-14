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

namespace MoDi.Core;

/// <summary>
/// ILogger — 统一日志接口
/// </summary>
public interface ILogger
{
    void Debug(string tag, string msg);
    void Info(string tag, string msg);
    void Warn(string tag, string msg);
    void Error(string tag, string msg);
    void Error(string tag, string msg, Exception ex);
}
