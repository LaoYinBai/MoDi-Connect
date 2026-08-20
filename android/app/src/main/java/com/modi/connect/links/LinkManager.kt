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
package com.modi.connect.links

import android.content.Context
import android.media.projection.MediaProjection
import com.modi.connect.ConnectionStateManager
import com.modi.connect.audio.AudioPipeline
import com.modi.connect.links.bluetooth.BluetoothLink
import com.modi.connect.links.usb.UsbLink
import com.modi.connect.links.wifidirect.WifiDirectLink
import com.modi.connect.links.wifilan.WifiLanLink
import com.modi.protocol.LinkType
import com.modi.connect.session.DisconnectReason
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.ensureActive
import kotlin.coroutines.coroutineContext

/**
 * LinkManager — 纯路由器 + 单链路互斥
 *
 * 职责：
 * 1. when(linkType) 分发到对应链路
 * 2. 强制同一时刻只有一条链路活跃（connect 前自动 disconnect 旧链路）
 * 3. 不含任何链路实现代码，不引用链路内部逻辑
 */
class LinkManager(
    context: Context,
    pipe: AudioPipeline,
    val stateManager: ConnectionStateManager
) {
    // ── 四级链路实例 ──
    val wifiLan = WifiLanLink(context, pipe, stateManager)
    val wifiDirect = WifiDirectLink(context, pipe, stateManager)
    val bluetooth = BluetoothLink(context, pipe, stateManager)
    val usb = UsbLink(context, pipe, stateManager)

    private var activeLink: ILink? = null
    private var connectingLink: ILink? = null

    val activeLinkType: Byte?
        get() = activeLink?.let { lastLinkType }

    /** 上一次活跃的链路类型（用于 UI 显示当前链路） */
    var lastLinkType: Byte = LinkType.WIFI_LAN
        private set

    // ── 统一入口（单链路互斥） ──

    /**
     * 连接指定链路。如果当前有其他链路活跃，先断开旧链路再连接新链路。
     * 四条链路互不知道对方存在，切换对链路透明。
     */
    suspend fun connect(linkType: Byte, params: LinkParams): Boolean {
        val link: ILink = when (linkType) {
            LinkType.WIFI_LAN -> wifiLan
            LinkType.WIFI_DIRECT -> wifiDirect
            LinkType.BLUETOOTH -> bluetooth
            LinkType.USB -> usb
            else -> return false
        }

        // 单链路互斥：连接前释放旧链路；同链路异常后的“重试”也不能复用残留传输。
        activeLink?.disconnect()
        activeLink = null

        lastLinkType = linkType
        connectingLink = link
        return try {
            val ok = link.connect(params)
            coroutineContext.ensureActive()
            if (ok) activeLink = link
            ok
        } catch (cancelled: CancellationException) {
            link.disconnect()
            throw cancelled
        } finally {
            if (connectingLink === link) connectingLink = null
        }
    }

    /**
     * 重连：沿用上一次的链路类型，参数由调用方传入。
     * LinkManager 不关心链路内部如何获取 token/host。
     */
    suspend fun reconnect(params: LinkParams): Boolean {
        return connect(lastLinkType, params)
    }

    suspend fun sendRouteUpdate(route: Int, proj: MediaProjection?): Boolean {
        return activeLink?.sendRouteUpdate(route, proj) ?: false
    }

    suspend fun notifyDisconnect(targetLink: Byte, reason: DisconnectReason): Boolean =
        activeLink?.sendDisconnectRequest(targetLink, reason) ?: false

    suspend fun disconnect() {
        val pending = connectingLink
        connectingLink = null
        pending?.disconnect()
        activeLink?.takeIf { it !== pending }?.disconnect()
        activeLink = null
    }

    suspend fun disconnectActive() = disconnect()

    fun cancelPendingConnection() {
        connectingLink = null
    }

    fun forgetWifiDirectPeer() = wifiDirect.forgetRemotePeer()

    // ── 状态查询 ──

    val isStreaming: Boolean get() = activeLink?.isStreaming ?: false

    companion object {
        /** 路线编号 → 采集模式（共享工具，不属于任何链路） */
        fun routeToCapture(r: Int): Int = when (r) {
            0, 3 -> AudioPipeline.MODE_SYSTEM
            1 -> AudioPipeline.MODE_MIX
            else -> AudioPipeline.MODE_MIC
        }
    }
}
