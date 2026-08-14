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

import com.modi.connect.core.enums.NetworkQuality
import com.modi.protocol.TransportType
import com.modi.connect.core.models.NetworkInfo

/**
 * INetworkMonitor — 统一网络状态监听接口
 *
 * 职责仅限"网络状态监听"，不含重连逻辑。
 * 重连逻辑由 ReconnectionManager 基于此接口的回调触发。
 *
 * 当前实现：
 *   Android — AndroidNetworkMonitor（基于 ConnectivityManager.NetworkCallback）
 *   Windows — WinNetworkMonitor（基于 NetworkChange）
 */
interface INetworkMonitor {
    /** 开始监听网络状态变化 */
    fun start()

    /** 停止监听 */
    fun stop()

    /** 当前是否有网络连接 */
    val isConnected: Boolean

    /** 当前活跃的传输类型 */
    val activeTransport: TransportType

    /** 当前网络质量 */
    val quality: NetworkQuality

    /** 网络状态变化时回调 */
    var onNetworkChanged: ((NetworkInfo) -> Unit)?
}
