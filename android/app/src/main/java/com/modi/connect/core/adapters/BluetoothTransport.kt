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

import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothSocket
import com.modi.protocol.TransportType
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.core.TransportIdentity
import com.modi.protocol.ITransport
import com.modi.protocol.StreamFrameDecoder
import kotlinx.coroutines.*
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.util.UUID

/**
 * BluetoothTransport — ITransport 的 RFCOMM 实现（Android 端，Client 角色）
 *
 * 职责：主动连接 Windows RFCOMM Server，在字节流上实现 PacketHeader 帧分割。
 *
 * 帧分割协议（与 Windows 端一致）：
 *   发送：直接写入 [15B header + payload] 完整字节数组
 *   接收：读 15B → 解析 PayloadLength → 再读 payload → 触发 onPacketReceived
 *
 * 生命周期：
 *   connectTo(device) → 连接到 Windows 蓝牙服务
 *   连接建立后自动启动帧分割读取循环
 *   disconnect() → 关闭连接
 */
class BluetoothTransport : ITransport {

    companion object {
        private const val TAG = "BluetoothTransport"

        /** 自定义服务 UUID（与 Windows 端一致） */
        val SERVICE_UUID: UUID = TransportIdentity.BLUETOOTH_SERVICE_UUID
    }

    private var socket: BluetoothSocket? = null
    private var inputStream: InputStream? = null
    private var outputStream: OutputStream? = null
    private var scope: CoroutineScope? = null
    @Volatile private var _isConnected = false

    // ── ITransport 实现 ──

    override var onPacketReceived: ((ByteArray) -> Unit)? = null
    override val isConnected: Boolean get() = _isConnected
    override val type: TransportType = TransportType.Bluetooth

    /**
     * 连接到 Windows RFCOMM Server。
     * 使用 createInsecureRfcommSocketToServiceRecord（不触发系统配对弹窗，依赖已有配对）。
     * @param device 已配对的 Windows 蓝牙设备
     * @return true=连接成功
     */
    fun connectTo(device: BluetoothDevice): Boolean {
        if (_isConnected) return true

        return try {
            val s = device.createInsecureRfcommSocketToServiceRecord(SERVICE_UUID)
            socket = s
            s.connect()
            inputStream = s.inputStream
            outputStream = s.outputStream
            _isConnected = true

            scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
            scope!!.launch { receiveLoop() }

            Log.i(TAG, "Connected to Windows RFCOMM: ${device.address}")
            true
        } catch (e: IOException) {
            Log.e(TAG, "connectTo failed: ${e.message}")
            closeSocket()
            false
        } catch (e: SecurityException) {
            Log.e(TAG, "Bluetooth permission denied: ${e.message}")
            false
        }
    }

    /** ITransport.connect — 蓝牙链路使用 connectTo(device)，此方法为接口兼容保留 */
    override suspend fun connect() { }

    /** 断开 RFCOMM 连接，停止读取循环，释放流和 socket */
    override suspend fun disconnect() {
        _isConnected = false

        scope?.cancel()
        scope = null
        closeSocket()

        Log.i(TAG, "Disconnected")
    }

    override suspend fun send(data: ByteArray) {
        sendBlocking(data)
    }

    /**
     * 阻塞发送（供非协程的音频采集线程使用）
     * 写入完整包字节数组到 RFCOMM 流，synchronized 保证线程安全
     */
    fun sendBlocking(data: ByteArray) {
        if (!_isConnected) return
        try {
            outputStream?.let { out ->
                synchronized(out) {
                    out.write(data)
                    out.flush()
                }
            }
        } catch (e: IOException) {
            Log.e(TAG, "sendBlocking error: ${e.message}")
            _isConnected = false
        }
    }

    // ── 流式帧分割接收循环（使用协议层共享 StreamFrameDecoder，与 Windows 端对称） ──

    private suspend fun receiveLoop() {
        val input = inputStream ?: return
        StreamFrameDecoder.runLoop(
            stream = input,
            onPacket = { onPacketReceived?.invoke(it) },
            onError = { if (_isConnected) Log.w(TAG, it) },
            isActive = { _isConnected }
        )
        _isConnected = false
        Log.i(TAG, "receiveLoop exited")
    }

    private fun closeSocket() {
        try { inputStream?.close() } catch (_: Exception) {}
        try { outputStream?.close() } catch (_: Exception) {}
        try { socket?.close() } catch (_: Exception) {}
        inputStream = null
        outputStream = null
        socket = null
    }
}
