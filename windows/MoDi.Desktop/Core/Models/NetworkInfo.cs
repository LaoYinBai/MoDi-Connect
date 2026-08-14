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
using MoDi.Protocol;

namespace MoDi.Core;

/// <summary>
/// 网络状态信息快照
/// </summary>
public struct NetworkInfo
{
    /// <summary>是否已连接</summary>
    public bool IsConnected;

    /// <summary>当前活跃的传输类型</summary>
    public TransportType TransportType;

    /// <summary>网络质量</summary>
    public NetworkQuality Quality;

    /// <summary>WiFi SSID（如有）</summary>
    public string? Ssid;

    /// <summary>网络接口名</summary>
    public string? InterfaceName;
}
