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
namespace MoDi.Core;

/// <summary>
/// 网络质量等级枚举
/// </summary>
public enum NetworkQuality
{
    /// <summary>未知</summary>
    Unknown,

    /// <summary>优秀（局域网低延迟）</summary>
    Excellent,

    /// <summary>良好（WiFi 已连接且互联网可达）</summary>
    Good,

    /// <summary>较差（仅蜂窝数据或信号弱）</summary>
    Poor,

    /// <summary>已断开</summary>
    Disconnected
}
