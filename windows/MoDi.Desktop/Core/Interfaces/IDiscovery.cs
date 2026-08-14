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
/// IDiscovery — 统一设备发现接口（发现方视角）
///
/// 当前实现：
///   Android — NsdDiscoveryAdapter（NsdManager 扫描 _modi._udp）
///   Windows — WinDiscovery（桩实现，Windows 当前是被发现方，不主动发现）
///
/// 设计说明：
///   Windows 端的 mDNS 发布功能保留在 MdnsPublisher 中，
///   此接口只承诺"发现"能力。以后 Windows 也需要发现设备时再实现。
/// </summary>
public interface IDiscovery
{
    /// <summary>开始发现</summary>
    void Start();

    /// <summary>停止发现</summary>
    void Stop();

    /// <summary>发现新设备时触发</summary>
    event Action<DeviceInfo>? OnDeviceFound;

    /// <summary>设备丢失时触发</summary>
    event Action<DeviceInfo>? OnDeviceLost;
}
