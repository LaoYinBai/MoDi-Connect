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
/// 发现的设备信息
/// </summary>
public struct DeviceInfo
{
    /// <summary>设备名称</summary>
    public string Name;

    /// <summary>IP 地址</summary>
    public string Ip;

    /// <summary>服务端口</summary>
    public int Port;

    /// <summary>传输类型</summary>
    public TransportType Transport;
}
