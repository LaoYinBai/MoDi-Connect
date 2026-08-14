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
package com.modi.connect.core.interfaces

import com.modi.connect.core.models.DeviceInfo

/**
 * IDiscovery — 统一设备发现接口（发现方视角）
 *
 * 当前实现：
 *   Android — NsdDiscoveryAdapter（NsdManager 扫描 _modi._udp）
 *   Windows — WinDiscovery（桩实现，Windows 是被发现方）
 */
interface IDiscovery {
    /** 开始发现 */
    fun start()

    /** 停止发现 */
    fun stop()

    /** 发现新设备时回调 */
    var onDeviceFound: ((DeviceInfo) -> Unit)?

    /** 设备丢失时回调 */
    var onDeviceLost: ((DeviceInfo) -> Unit)?
}
