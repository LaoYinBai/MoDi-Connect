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
package com.modi.connect.links.bluetooth

import android.annotation.SuppressLint
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothClass
import android.bluetooth.BluetoothDevice
import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjection
import com.modi.connect.ConnectionState
import com.modi.connect.ConnectionStateManager
import com.modi.connect.StreamingService
import com.modi.connect.audio.AudioPipeline
import com.modi.connect.core.adapters.BluetoothTransport
import com.modi.protocol.PacketHeaderCodec
import com.modi.connect.core.infrastructure.Log
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
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.util.UUID

/**
 * BluetoothLink — 蓝牙 RFCOMM 链路（Android 端，主动发起方）
 *
 * 职责：发现已配对 Windows 设备 → 连接 RFCOMM → 发 HELLO → 推流。
 * 与 LAN 模式对称：手机点按钮主动发起连接。
 *
 * 握手方向：Android 发 HELLO(token) → Windows 校验 → 回 HELLO_ACK(route)
 * 数据通路：AudioPipeline → EncodeSender → BluetoothTransport.sendBlocking()
 */
class BluetoothLink(
    private val context: Context,
    private val pipe: AudioPipeline,
    private val stateManager: ConnectionStateManager
) : ILink {

    companion object {
        private const val TAG = "BluetoothLink"
    }

    // ── 子模块 ──
    private val connectMutex = Mutex()
    private var btTransport: BluetoothTransport? = null

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
     * 连接蓝牙链路（用户点击“蓝牙直连”触发）
     * 流程：查找已配对电脑 → RFCOMM 连接 → 发 HELLO 握手 → 启动推流
     */

    @SuppressLint("MissingPermission")
    override suspend fun connect(params: LinkParams): Boolean = connectMutex.withLock {
        currentRoute = params.route
        onStatusChanged?.invoke("蓝牙：搜索已配对的电脑...")
        stateManager.update(ConnectionState.CONNECTING)

        // 1. 查找已配对的 Windows 设备
        val device = findPairedWindowsDevice()
        if (device == null) {
            onStatusChanged?.invoke("蓝牙：未找到已配对的电脑（请先在系统设置中配对）")
            stateManager.update(ConnectionState.ERROR)
            return@withLock false
        }
        Log.i(TAG, "Found paired device: ${device.name} (${device.address})")
        onStatusChanged?.invoke("蓝牙：连接 ${device.name}...")

        // 2. 建立 RFCOMM 连接
        val transport = BluetoothTransport()
        btTransport = transport
        val connected = withContext(Dispatchers.IO) { transport.connectTo(device) }
        if (!connected) {
            btTransport = null
            onStatusChanged?.invoke("蓝牙：连接失败（电脑端是否已启动？）")
            stateManager.update(ConnectionState.ERROR)
            return@withLock false
        }
        onStatusChanged?.invoke("蓝牙：已连接，握手中...")

        // 3. 主动发 HELLO → 等待 ACK
        val handshake = withContext(Dispatchers.IO) {
            BtHandshakeClient.sendHelloAndWaitForAck(transport, params.route, UUID.randomUUID())
        }
        if (handshake == null) {
            onStatusChanged?.invoke("蓝牙：握手失败（电脑未响应）")
            stateManager.update(ConnectionState.ERROR)
            disconnect()
            return@withLock false
        }

        stateManager.update(ConnectionState.CONNECTED)
        sessionId = handshake.sessionId
        onStatusChanged?.invoke("蓝牙：握手成功 ✓ 准备推流")

        // 4. 启动推流（注入 BluetoothTransport）
        val capMode = LinkManager.routeToCapture(params.route)
        pipe.onFirstFrame = { stateManager.update(ConnectionState.STREAMING) }
        val ok = withContext(Dispatchers.IO) {
            pipe.currentLinkType = LinkType.BLUETOOTH
            pipe.startStreamingWithTransport(transport, capMode, params.proj, context)
        }

        if (ok) {
            isStreaming = true
            onStatusChanged?.invoke("蓝牙推流中：路线${params.route + 1}")
            onStreamingChanged?.invoke(true)
            context.startForegroundService(Intent(context, StreamingService::class.java))
        } else {
            onStatusChanged?.invoke("蓝牙：启动推流失败")
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
        val transport = btTransport ?: return false
        withContext(Dispatchers.IO) {
            val protocol = PacketHeaderCodec()
            val payload = byteArrayOf(route.toByte())
            val packet = Packet(PacketType.ROUTE, LinkType.BLUETOOTH, 0u, payload)
            transport.sendBlocking(protocol.encode(packet))
        }
        return true
    }

    override suspend fun sendDisconnectRequest(targetLink: Byte, reason: DisconnectReason): Boolean {
        val currentId = sessionId ?: return false
        val transport = btTransport?.takeIf { it.isConnected } ?: return false
        val message = SessionControlMessage.request(currentId, LinkType.BLUETOOTH, targetLink, reason)
        return withContext(Dispatchers.IO) {
            val encoded = PacketHeaderCodec().encode(message.toPacket())
            transport.sendBlocking(encoded)
            transport.isConnected
        }
    }

    /** 断开蓝牙链路：停止推流 + 关闭 RFCOMM + 状态回退 */
    override fun disconnect() {
        context.stopService(Intent(context, StreamingService::class.java))
        pipe.stopStreaming()

        btTransport?.let { t ->
            kotlinx.coroutines.runBlocking { t.disconnect() }
        }
        btTransport = null
        sessionId = null

        isStreaming = false
        stateManager.update(ConnectionState.DISCONNECTED)
        onStatusChanged?.invoke("蓝牙：已停止")
        onStreamingChanged?.invoke(false)
    }

    // ── 发现已配对设备（按蓝牙设备类过滤，只连电脑类设备） ──

    @SuppressLint("MissingPermission")
    private fun findPairedWindowsDevice(): BluetoothDevice? {
        val adapter = BluetoothAdapter.getDefaultAdapter() ?: return null
        if (!adapter.isEnabled) return null

        val bondedDevices = adapter.bondedDevices ?: return null

        // 优先找电脑类设备（Major Class = COMPUTER）
        val computer = bondedDevices.firstOrNull { device ->
            device.bluetoothClass?.majorDeviceClass == BluetoothClass.Device.Major.COMPUTER
        }
        if (computer != null) return computer

        // 找不到电脑类设备时返回 null（不连耳机/音箱）
        Log.w(TAG, "No COMPUTER-class device found in ${bondedDevices.size} paired devices")
        return null
    }

}
