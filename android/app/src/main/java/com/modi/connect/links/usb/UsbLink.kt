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
package com.modi.connect.links.usb

import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjection
import com.modi.connect.ConnectionState
import com.modi.connect.ConnectionStateManager
import com.modi.connect.StreamingService
import com.modi.connect.audio.AudioPipeline
import com.modi.protocol.PacketHeaderCodec
import com.modi.connect.core.adapters.UsbTransport
import com.modi.protocol.Packet
import com.modi.connect.links.ILink
import com.modi.connect.links.LinkState
import com.modi.connect.links.LinkManager
import com.modi.connect.links.LinkParams
import com.modi.protocol.LinkType
import com.modi.protocol.PacketType
import com.modi.connect.session.DisconnectReason
import com.modi.connect.session.SessionControlMessage
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.delay
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.util.UUID

/**
 * UsbLink — USB 链路（Android 端，主动发起方）
 *
 * 职责：用户点击"USB 直连" → TCP 连接 localhost:12348 → 发 HELLO → 推流。
 * 与蓝牙链路对称：手机主动发起连接和握手，Windows 常驻等待。
 *
 * 握手方向：Android 发 HELLO(token+route) → Windows 校验 → 回 HELLO_ACK(route)（与蓝牙一致）
 * 数据通路：AudioPipeline → EncodeSender → UsbTransport.sendBlocking()
 *
 * 依赖：USB 链路专属，与 LAN/P2P/蓝牙完全解耦。
 * 前置条件：USB 线已连接 + USB 调试已开启 + Windows 端已自动建立 adb forward。
 */
class UsbLink(
    private val context: Context,
    private val pipe: AudioPipeline,
    private val stateManager: ConnectionStateManager
) : ILink {

    companion object {
        private const val TAG = "UsbLink"
    }

    // ── 子模块 ──
    private val connectMutex = Mutex()
    private var usbTransport: UsbTransport? = null

    // ── ILink 状态 ──
    @Volatile override var isStreaming = false
        private set
    override val state: LinkState
        get() = if (isStreaming) LinkState.STREAMING else LinkState.IDLE
    override var onStatusChanged: ((String) -> Unit)? = null
    override var onStreamingChanged: ((Boolean) -> Unit)? = null
    @Volatile override var sessionId: UUID? = null
        private set

    @Volatile var currentRoute: Int = 0
        private set

    // ── ILink 实现 ──

    /**
     * 连接 USB 链路（用户点击“USB 直连”触发）
     * 流程：启动 TCP Server → 等待 Windows 连接 → 发 HELLO → 等 ACK → 推流
     */
    override suspend fun connect(params: LinkParams): Boolean = connectMutex.withLock {
        currentRoute = params.route
        onStatusChanged?.invoke("USB：启动监听，请确认 USB 已连接且电脑端已就绪...")
        stateManager.update(ConnectionState.CONNECTING)
    
        // 1. 启动 TCP Server（等待 Windows 通过 adb forward 连入）
        val transport = UsbTransport()
        transport.startListening()
        usbTransport = transport
    
        val connected = try {
            // 等待 ServerSocket 绑定完成
            delay(200)

            // 2. 等待 Windows 连接
            onStatusChanged?.invoke("USB：等待电脑连接...")
            transport.waitForConnection(timeoutMs = 0)
        } catch (cancelled: CancellationException) {
            transport.stopListening()
            usbTransport = null
            sessionId = null
            throw cancelled
        }
        if (!connected) {
            onStatusChanged?.invoke("USB：等待连接超时")
            stateManager.update(ConnectionState.ERROR)
            transport.stopListening()
            usbTransport = null
            return@withLock false
        }
        onStatusChanged?.invoke("USB：电脑已连接，握手中...")

        // 2. 主动发 HELLO → 等待 ACK（与蓝牙 BtHandshakeClient 对称）
        val handshake = withContext(Dispatchers.IO) {
            UsbHandshakeClient.sendHelloAndWaitForAck(transport, params.route, UUID.randomUUID())
        }
        if (handshake == null) {
            onStatusChanged?.invoke("USB：握手失败（电脑未响应）")
            stateManager.update(ConnectionState.ERROR)
            disconnect()
            return@withLock false
        }

        stateManager.update(ConnectionState.CONNECTED)
        sessionId = handshake.sessionId
        onStatusChanged?.invoke("USB：握手成功 ✓ 准备推流")

        // 3. 启动推流（注入 UsbTransport）
        val capMode = LinkManager.routeToCapture(params.route)
        pipe.onFirstFrame = { stateManager.update(ConnectionState.STREAMING) }
        val ok = withContext(Dispatchers.IO) {
            pipe.currentLinkType = LinkType.USB
            pipe.startStreamingWithTransport(transport, capMode, params.proj, context)
        }

        if (ok) {
            isStreaming = true
            onStatusChanged?.invoke("USB 推流中：路线${params.route + 1}")
            onStreamingChanged?.invoke(true)
            context.startForegroundService(Intent(context, StreamingService::class.java))
        } else {
            onStatusChanged?.invoke("USB：启动推流失败")
            stateManager.update(ConnectionState.ERROR)
            disconnect()
        }
        ok
    }

    /** 推流中热切路线：切换采集模式 + 发送 ROUTE 包通知 Windows 切换 AudioRouter */
    override suspend fun sendRouteUpdate(route: Int, proj: MediaProjection?): Boolean {
        if (!isStreaming) { currentRoute = route; return true }
        currentRoute = route

        val capMode = LinkManager.routeToCapture(route)
        val ok = withContext(Dispatchers.IO) { pipe.switchMode(capMode, proj, context) }
        if (!ok) { onStatusChanged?.invoke("需先授权系统音频"); return false }

        // 发送 ROUTE 包到 Windows
        val transport = usbTransport ?: return false
        withContext(Dispatchers.IO) {
            val protocol = PacketHeaderCodec()
            val payload = byteArrayOf(route.toByte())
            val packet = Packet(PacketType.ROUTE, LinkType.USB, 0u, payload)
            transport.sendBlocking(protocol.encode(packet))
        }
        return true
    }

    override suspend fun sendDisconnectRequest(targetLink: Byte, reason: DisconnectReason): Boolean {
        val currentId = sessionId ?: return false
        val transport = usbTransport?.takeIf { it.isConnected } ?: return false
        val message = SessionControlMessage.request(currentId, LinkType.USB, targetLink, reason)
        return withContext(Dispatchers.IO) {
            val encoded = PacketHeaderCodec().encode(message.toPacket())
            transport.sendBlocking(encoded)
            transport.isConnected
        }
    }

    /** 断开 USB 链路：停止推流 + 关闭 TCP Server + 状态回退 */
    override suspend fun disconnect() {
        context.stopService(Intent(context, StreamingService::class.java))
        pipe.stopStreaming()

        usbTransport?.stopListening()
        usbTransport = null
        sessionId = null

        isStreaming = false
        stateManager.update(ConnectionState.DISCONNECTED)
        onStatusChanged?.invoke("USB：已停止")
        onStreamingChanged?.invoke(false)
    }

}
