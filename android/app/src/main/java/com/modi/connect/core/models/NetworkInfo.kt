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
package com.modi.connect.core.models

import com.modi.connect.core.enums.NetworkQuality
import com.modi.protocol.TransportType

/**
 * 网络状态信息快照
 */
data class NetworkInfo(
    /** 是否已连接 */
    val isConnected: Boolean,
    /** 当前活跃的传输类型 */
    val transportType: TransportType = TransportType.Udp,
    /** 网络质量 */
    val quality: NetworkQuality = NetworkQuality.Unknown,
    /** WiFi SSID（如有） */
    val ssid: String? = null,
    /** 网络接口名 */
    val interfaceName: String? = null
)
