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
package com.modi.connect.core.adapters

import android.content.Context
import com.modi.protocol.TransportType
import com.modi.connect.core.interfaces.IDiscovery
import com.modi.connect.core.models.DeviceInfo
import com.modi.connect.net.MoDiDiscovery

/**
 * NsdDiscoveryAdapter — mDNS 设备发现适配器
 *
 * 包裹 [MoDiDiscovery]，实现 [IDiscovery]。
 * 将 NsdManager 的发现回调桥接到统一接口。
 */
class NsdDiscoveryAdapter(
    private val context: Context
) : IDiscovery {

    private var discovery: MoDiDiscovery? = null

    override var onDeviceFound: ((DeviceInfo) -> Unit)? = null
    override var onDeviceLost: ((DeviceInfo) -> Unit)? = null

    override fun start() {
        if (discovery != null) return

        val d = MoDiDiscovery(context)
        d.setOnDeviceFound { device ->
            onDeviceFound?.invoke(
                DeviceInfo(
                    name = device.name,
                    ip = device.host,
                    port = device.port,
                    transport = TransportType.Udp
                )
            )
        }
        d.setOnDeviceLost { device ->
            onDeviceLost?.invoke(
                DeviceInfo(
                    name = device.name,
                    ip = device.host,
                    port = device.port,
                    transport = TransportType.Udp
                )
            )
        }
        d.startScan()
        discovery = d
    }

    override fun stop() {
        discovery?.stopScan()
        discovery = null
    }
}
