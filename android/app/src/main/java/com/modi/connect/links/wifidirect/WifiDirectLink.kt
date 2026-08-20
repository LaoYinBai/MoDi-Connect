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
package com.modi.connect.links.wifidirect

import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjection
import com.modi.connect.ConnectionState
import com.modi.connect.ConnectionStateManager
import com.modi.connect.StreamingService
import com.modi.connect.audio.AudioPipeline
import com.modi.connect.core.TransportIdentity
import com.modi.connect.links.ILink
import com.modi.connect.links.LinkState
import com.modi.connect.links.LinkManager
import com.modi.connect.links.LinkParams
import com.modi.connect.net.HandshakeManager
import com.modi.protocol.LinkType
import com.modi.connect.net.P2pPairStore
import com.modi.connect.net.WifiDirectManager
import com.modi.connect.session.DisconnectReason
import com.modi.connect.session.SessionControlMessage
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.util.UUID

/**
 * WifiDirectLink — WiFi Direct P2P 链路（完整实现）
 *
 * 职责：扫码 → createGroup → 等待 Windows HELLO → 推流。
 * 与 LAN / 蓝牙 / USB 完全解耦。
 */
class WifiDirectLink(
    private val context: Context,
    private val pipe: AudioPipeline,
    private val stateManager: ConnectionStateManager
) : ILink {

    companion object {
        const val AUDIO_PORT = TransportIdentity.AUDIO_PORT
        const val HANDSHAKE_PORT = TransportIdentity.HANDSHAKE_PORT
        const val HANDSHAKE_TIMEOUT_MS = 3000L
        const val HANDSHAKE_MAX_ATTEMPTS = 3
        const val IP_POLL_INTERVAL_MS = 500L
        const val IP_POLL_MAX_RETRIES = 16
    }

    // ── 子模块 ──
    val wifiDirectManager = WifiDirectManager(context)
    private val connectMutex = Mutex()  // 防止并发 connect（扫码过快时两次调用冲突）

    // ── ILink 状态 ──
    @Volatile override var isStreaming = false
        private set
    override val state: LinkState
        get() = if (isStreaming) LinkState.STREAMING else LinkState.IDLE
    override var onStatusChanged: ((String) -> Unit)? = null
    override var onStreamingChanged: ((Boolean) -> Unit)? = null
    @Volatile override var sessionId: UUID? = null
        private set

    // ── P2P 特有状态（链路自治，不依赖外部） ──
    @Volatile var p2pTargetIp: String? = null
        private set
    @Volatile var currentRoute: Int = 0
    private var p2pLocalIp: String? = null       // Android P2P 接口 IP（GO IP）
    private var lastRemoteIp: String? = null     // 上次握手的 Windows IP（重连用）

    // ── ILink 实现 ──

    /**
     * 连接 P2P 链路（用户扫码触发）
     * 流程：获取 token → createGroup → 等待 Windows 连接 → 握手 → 推流
     * 重连策略：已知 Windows IP 时主动发 HELLO，否则被动等待
     */
    override suspend fun connect(params: LinkParams): Boolean = connectMutex.withLock {
        // token 来源：扫码传入 或 已配对存储（冷启动免扫码）
        val token = params.token
            ?: P2pPairStore.load(context)?.token
            ?: return@withLock false
        currentRoute = params.route

        onStatusChanged?.invoke("正在创建 P2P Group...")
        val goIp = wifiDirectManager.createGroupAndWaitForClient()
        if (goIp == null) {
            onStatusChanged?.invoke("P2P Group 创建失败")
            stateManager.update(ConnectionState.ERROR)
            return@withLock false
        }
        p2pLocalIp = goIp

        stateManager.update(ConnectionState.CONNECTING)

        // 重连策略：已知 Windows IP 时主动发 HELLO，否则被动等待
        var establishedSessionId: UUID? = null
        val handshakeOk = if (lastRemoteIp != null) {
            onStatusChanged?.invoke("P2P 重连中，主动握手...")
            val newSessionId = UUID.randomUUID()
            withContext(Dispatchers.IO) {
                HandshakeManager.handshake(
                    lastRemoteIp!!,
                    params.route,
                    token,
                    LinkType.WIFI_DIRECT,
                    p2pLocalIp,
                    newSessionId
                ).also { if (it) establishedSessionId = newSessionId }
            }
        } else {
            onStatusChanged?.invoke("P2P GO 就绪 ($goIp)，等待电脑连接...")
            val remoteIp = withContext(Dispatchers.IO) {
                P2pHandshakeServer.waitForHello(token, params.route)
            }
            if (remoteIp != null) {
                lastRemoteIp = remoteIp.remoteIp
                establishedSessionId = remoteIp.sessionId
                true
            } else false
        }
        if (!handshakeOk) {
            onStatusChanged?.invoke("P2P 握手失败（电脑未响应）")
            stateManager.update(ConnectionState.ERROR)
            return@withLock false
        }

        stateManager.update(ConnectionState.CONNECTED)
        sessionId = establishedSessionId
        onStatusChanged?.invoke("P2P 握手成功 ✓ 准备推流")

        // 配对持久化：握手成功即写入，后续冷启动免扫码
        P2pPairStore.save(context, token, params.deviceName ?: "")

        val winP2pIp = lastRemoteIp ?: goIp
        p2pTargetIp = winP2pIp
        val capMode = LinkManager.routeToCapture(params.route)

        pipe.onFirstFrame = { stateManager.update(ConnectionState.STREAMING) }
        val ok = withContext(Dispatchers.IO) {
            pipe.currentLinkType = LinkType.WIFI_DIRECT
            pipe.startStreaming(capMode, params.proj, context, winP2pIp, localBindAddress = p2pLocalIp)
        }

        if (ok) {
            isStreaming = true
            onStatusChanged?.invoke("P2P 推流中：路线${params.route + 1}")
            onStreamingChanged?.invoke(true)
            context.startForegroundService(Intent(context, StreamingService::class.java))
        } else {
            onStatusChanged?.invoke("P2P 启动推流失败")
            stateManager.update(ConnectionState.ERROR)
        }
        ok
    }

    /** 推流中热切路线：切换采集模式 + 通过 UDP 发送 ROUTE 包到 Windows */
    override suspend fun sendRouteUpdate(route: Int, proj: MediaProjection?): Boolean {
        if (!isStreaming) { currentRoute = route; return true }
        currentRoute = route

        val capMode = LinkManager.routeToCapture(route)
        val ok = withContext(Dispatchers.IO) { pipe.switchMode(capMode, proj, context) }
        if (!ok) { onStatusChanged?.invoke("需先授权系统音频"); return false }

        val targetIp = p2pTargetIp ?: return false
        withContext(Dispatchers.IO) { HandshakeManager.sendRouteUpdate(targetIp, route, LinkType.WIFI_DIRECT, p2pLocalIp) }
        return true
    }

    override suspend fun sendDisconnectRequest(targetLink: Byte, reason: DisconnectReason): Boolean {
        val currentId = sessionId ?: return false
        val targetIp = p2pTargetIp ?: return false
        val message = SessionControlMessage.request(currentId, LinkType.WIFI_DIRECT, targetLink, reason)
        return withContext(Dispatchers.IO) {
            HandshakeManager.sendSessionControl(targetIp, message, p2pLocalIp)
        }
    }

    /** 断开 P2P 链路：停止推流 + 销毁 P2P Group + 状态回退 */
    override fun disconnect() {
        context.stopService(Intent(context, StreamingService::class.java))
        pipe.stopStreaming()
        wifiDirectManager.disconnect()
        isStreaming = false
        p2pTargetIp = null
        p2pLocalIp = null
        sessionId = null
        stateManager.update(ConnectionState.DISCONNECTED)
        onStatusChanged?.invoke("已停止")
        onStreamingChanged?.invoke(false)
    }

    /** 仅在显式重新配对时清除旧电脑，普通断线保留该地址用于快速重连。 */
    fun forgetRemotePeer() {
        lastRemoteIp = null
    }

}
