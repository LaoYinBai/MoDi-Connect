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
using MoDi.Core.Infrastructure;

namespace MoDi.Core.Adapters;

/// <summary>
/// WinDiscovery — Windows 设备发现桩实现
///
/// Windows 当前是被发现方（通过 MdnsPublisher 发布 mDNS 服务），
/// 不主动发现其他设备。此桩实现满足接口完整性。
///
/// 以后 Windows 也需要发现设备时（如多电脑场景），再实现真正的发现逻辑。
/// </summary>
public sealed class WinDiscovery : IDiscovery
{
    private const string Tag = "WinDiscovery";

    public event Action<DeviceInfo>? OnDeviceFound;
    public event Action<DeviceInfo>? OnDeviceLost;

    public void Start()
    {
        Log.I(Tag, "WinDiscovery started (stub - Windows is the discovered party)");
    }

    public void Stop()
    {
        Log.I(Tag, "WinDiscovery stopped");
    }
}
