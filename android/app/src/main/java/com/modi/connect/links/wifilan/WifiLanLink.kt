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
package com.modi.connect.links.wifilan

import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjection
import com.modi.connect.ConnectionState
import com.modi.connect.ConnectionStateManager
import com.modi.connect.StreamingService
import com.modi.connect.audio.AudioPipeline
import com.modi.connect.core.factory.PlatformFactory
import com.modi.connect.core.TransportIdentity
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.links.ILink
import com.modi.connect.links.LinkParams
import com.modi.connect.links.LinkState
import com.modi.connect.net.HandshakeManager
import com.modi.connect.net.MoDiDiscovery
import com.modi.protocol.LinkType
import com.modi.connect.net.ReconnectionManager
import com.modi.connect.session.DisconnectReason
import com.modi.connect.session.SessionControlMessage
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.util.UUID

/**
 * WifiLanLink — WiFi LAN 链路（完整实现）
 *
 * 职责：mDNS 发现 + 握手 + 推流 + 断线重连。
 * 与 WiFi Direct / 蓝牙 / USB 完全解耦。
 *
 * 数据通路：AudioPipeline → EncodeSender → UdpTransport.sendBlocking() → Windows UDP 12345
 * 握手方向：Android 发 HELLO(route) → Windows 回 HELLO_ACK（与蓝牙一致）
 * 发现机制：NsdManager 扫描 _modi._udp 服务
 * 重连机制：ReconnectionManager 监听网络状态，断线后自动重试
 */
class WifiLanLink(
    private val context: Context,
    private val pipe: AudioPipeline,
    private val stateManager: ConnectionStateManager
) : ILink {

    companion object {
        const val AUDIO_PORT = TransportIdentity.AUDIO_PORT
        const val HANDSHAKE_PORT = TransportIdentity.HANDSHAKE_PORT
        const val MDNS_SERVICE_TYPE = TransportIdentity.MDNS_SERVICE_TYPE
        const val HANDSHAKE_TIMEOUT_MS = 500L
    }

    // ── 子模块 ──
    private val discovery = MoDiDiscovery(context)
    private val reconnectionManager: ReconnectionManager

    // ── ILink 状态 ──
    @Volatile override var isStreaming = false
        private set
    override val state: LinkState
        get() = if (isStreaming) LinkState.STREAMING else LinkState.IDLE
    override var onStatusChanged: ((String) -> Unit)? = null
    override var onStreamingChanged: ((Boolean) -> Unit)? = null
    @Volatile override var sessionId: UUID? = null
        private set

    // ── LAN 特有回调 ──
    var onDeviceFound: ((MoDiDiscovery.DeviceInfo) -> Unit)? = null
    var onDeviceLost: ((MoDiDiscovery.DeviceInfo) -> Unit)? = null

    @Volatile var currentTargetIp: String? = null
        private set
    @Volatile var currentRoute: Int = 0
    @Volatile private var currentProjection: MediaProjection? = null

    init {
        reconnectionManager = ReconnectionManager(
            stateManager = stateManager,
            stopStreaming = {
                isStreaming = false
                onStreamingChanged?.invoke(false)
                pipe.stopStreaming()
            },
            networkMonitor = PlatformFactory.createNetworkMonitor(context),
            onRecover = { host, mode ->
                val capMode = routeToCapture(mode)
                val recoveredSessionId = UUID.randomUUID()
                val ok = HandshakeManager.handshake(host, mode, sessionId = recoveredSessionId)
                if (!ok) return@ReconnectionManager false
                sessionId = recoveredSessionId
                // MoDiRuntime owns projection lifetime; its onStop cancels this session.
                val streamOk = pipe.startStreaming(capMode, currentProjection, context, host)
                if (!streamOk) {
                    Log.w("WifiLanLink", "重连后启动推流失败（route=$mode），交给重试循环")
                    return@ReconnectionManager false
                }
                isStreaming = true
                onStreamingChanged?.invoke(true)
                true
            }
        )
    }

    // ── 生命周期（mDNS 发现 + 重连监听，App 启动时调用） ──

    /** 启动 mDNS 扫描 + 断线重连监听（常驻，与推流状态无关） */
    fun start() {
        reconnectionManager.start()
        discovery.setOnDeviceFound { device ->
            onDeviceFound?.invoke(device)
            if (!isStreaming) {
                onStatusChanged?.invoke("发现电脑：${device.name}")
                stateManager.update(ConnectionState.FOUND)
            }
        }
        discovery.setOnDeviceLost { device -> onDeviceLost?.invoke(device) }
        discovery.setOnError { msg -> if (!isStreaming) onStatusChanged?.invoke("设备发现：$msg") }
        discovery.startScan()
    }

    /** 停止 mDNS 扫描 + 重连监听 */
    fun stop() {
        reconnectionManager.stop()
        discovery.stopScan()
    }

    // ── ILink 实现 ──

    /**
     * 连接 LAN 链路（用户选择发现的电脑后触发）
     * 流程：发 HELLO 握手 → 启动推流 → 记录目标 IP（供重连用）
     */
    override suspend fun connect(params: LinkParams): Boolean {
        reconnectionManager.cancelRecovery()
        val host = params.host ?: return false
        currentRoute = params.route
        currentProjection = params.proj
        val capMode = routeToCapture(params.route)

        stateManager.update(ConnectionState.CONNECTING)
        onStatusChanged?.invoke("正在握手...")

        val newSessionId = UUID.randomUUID()
        val handshakeOk = withContext(Dispatchers.IO) {
            HandshakeManager.handshake(host, params.route, sessionId = newSessionId)
        }
        if (!handshakeOk) {
            stateManager.update(ConnectionState.ERROR)
            onStatusChanged?.invoke("握手失败")
            return false
        }

        stateManager.update(ConnectionState.CONNECTED)
        sessionId = newSessionId
        pipe.onFirstFrame = { stateManager.update(ConnectionState.STREAMING) }
        val ok = withContext(Dispatchers.IO) {
            pipe.currentLinkType = LinkType.WIFI_LAN
            pipe.startStreaming(capMode, params.proj, context, host)
        }

        if (ok) {
            isStreaming = true
            currentTargetIp = host
            reconnectionManager.arm(host, params.route)
            onStatusChanged?.invoke("推流中：路线${params.route + 1} -> $host")
            onStreamingChanged?.invoke(true)
            context.startForegroundService(Intent(context, StreamingService::class.java))
        } else {
            stateManager.update(ConnectionState.ERROR)
            onStatusChanged?.invoke("启动推流失败")
        }
        return ok
    }

    /** 推流中热切路线：切换采集模式 + 通过 UDP 发送 ROUTE 包到 Windows */
    override suspend fun sendRouteUpdate(route: Int, proj: MediaProjection?): Boolean {
        if (!isStreaming) { currentRoute = route; return true }
        currentRoute = route

        val capMode = routeToCapture(route)
        val ok = withContext(Dispatchers.IO) { pipe.switchMode(capMode, proj, context) }
        if (!ok) { onStatusChanged?.invoke("需先授权系统音频"); return false }
        currentProjection = proj
        reconnectionManager.updateRoute(route)

        val targetIp = currentTargetIp ?: return false
        withContext(Dispatchers.IO) { HandshakeManager.sendRouteUpdate(targetIp, route, LinkType.WIFI_LAN) }
        return true
    }

    override suspend fun sendDisconnectRequest(targetLink: Byte, reason: DisconnectReason): Boolean {
        val currentId = sessionId ?: return false
        val targetIp = currentTargetIp ?: return false
        val message = SessionControlMessage.request(currentId, LinkType.WIFI_LAN, targetLink, reason)
        return withContext(Dispatchers.IO) { HandshakeManager.sendSessionControl(targetIp, message) }
    }

    /** 断开 LAN 链路：停止推流 + 清除重连记录 + 状态回退 */
    override suspend fun disconnect() {
        reconnectionManager.cancelRecovery()
        context.stopService(Intent(context, StreamingService::class.java))
        pipe.stopStreaming()
        isStreaming = false
        currentTargetIp = null
        sessionId = null
        currentProjection = null
        stateManager.update(ConnectionState.DISCONNECTED)
        onStatusChanged?.invoke("已停止")
        onStreamingChanged?.invoke(false)
    }

    // ── 工具：路线编号 → 采集模式（与 LinkManager.routeToCapture 一致） ──

    fun routeToCapture(r: Int) = when (r) {
        0, 3 -> AudioPipeline.MODE_SYSTEM
        1 -> AudioPipeline.MODE_MIX
        else -> AudioPipeline.MODE_MIC
    }
}
