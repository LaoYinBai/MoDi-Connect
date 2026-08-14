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

import com.modi.protocol.TransportType
import com.modi.connect.core.infrastructure.Log
import com.modi.protocol.ITransport
import com.modi.protocol.StreamFrameDecoder
import kotlinx.coroutines.*
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.net.ServerSocket
import java.net.Socket
import kotlin.coroutines.coroutineContext

/**
 * UsbTransport — ITransport 的 TCP 实现（Android 端，Server 角色）
 *
 * 职责：监听 localhost:12348，等待 Windows 通过 ADB forward 隧道连入，
 * 在字节流上实现 PacketHeader 帧分割（15B header + payload）。
 *
 * 数据流：
 *   Windows App (TCP Client) → localhost:12348 → [adb forward] → Android:12348 (本 Server)
 *   adb forward 由 Windows 端执行，将 Windows:12348 映射到 Android:12348。
 *
 * 帧分割协议（与 BluetoothTransport / Windows UsbTransport 一致）：
 *   发送：直接写入 [15B header + payload] 完整字节数组
 *   接收：读 15B → 解析 PayloadLength → 再读 payload → 触发 onPacketReceived
 *
 * 生命周期：
 *   startListening() → 启动 TCP ServerSocket 监听（后台协程）
 *   waitForConnection() → 等待 Windows 连入（阻塞）
 *   连接建立后自动启动帧分割读取循环
 *   disconnect() → 关闭当前连接
 *   stopListening() → 关闭 ServerSocket
 *
 * 依赖：USB 链路专属，与 LAN/P2P/蓝牙完全解耦。
 */
class UsbTransport : ITransport {

    companion object {
        private const val TAG = "UsbTransport"

        /** USB 链路 TCP 端口（adb forward 双端一致） */
        const val PORT = 12348
    }

    private var serverSocket: ServerSocket? = null
    private var clientSocket: Socket? = null
    private var inputStream: InputStream? = null
    private var outputStream: OutputStream? = null
    private var scope: CoroutineScope? = null
    @Volatile private var _isConnected = false
    @Volatile private var _listening = false

    // ── ITransport 实现 ──

    override var onPacketReceived: ((ByteArray) -> Unit)? = null
    override val isConnected: Boolean get() = _isConnected
    override val type: TransportType = TransportType.Usb

    /**
     * 启动 TCP Server 监听（后台协程，不阻塞）。
     * 用户点击"USB 直连"时调用，等待 Windows 通过 adb forward 连入。
     */
    fun startListening() {
        if (_listening) return
        _listening = true

        scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        scope!!.launch {
            try {
                serverSocket = ServerSocket(PORT)
                Log.i(TAG, "TCP server listening on port $PORT")
            } catch (e: IOException) {
                Log.e(TAG, "Failed to bind port $PORT: ${e.message}")
                _listening = false
            }
        }
    }

    /**
     * 等待 Windows 连接（阻塞直到有连接或超时）。
     * 连接建立后自动启动帧分割读取循环。
     * @param timeoutMs accept 超时（毫秒），0=无限等待
     * @return true=连接成功
     */
    suspend fun waitForConnection(timeoutMs: Int = 0): Boolean = withContext(Dispatchers.IO) {
        val ss = serverSocket ?: return@withContext false
        val cancellation = coroutineContext[Job]?.invokeOnCompletion {
            try { ss.close() } catch (_: Exception) {}
        }

        try {
            if (timeoutMs > 0) ss.soTimeout = timeoutMs
            val socket = ss.accept()
            coroutineContext.ensureActive()
            clientSocket = socket
            inputStream = socket.getInputStream()
            outputStream = socket.getOutputStream()
            _isConnected = true

            // 启动帧分割读取循环
            scope?.launch { receiveLoop() }

            Log.i(TAG, "Windows connected: ${socket.inetAddress}")
            true
        } catch (e: CancellationException) {
            throw e
        } catch (e: IOException) {
            coroutineContext.ensureActive()
            Log.w(TAG, "Accept timeout or error: ${e.message}")
            false
        } finally {
            cancellation?.dispose()
        }
    }

    /** ITransport.connect — USB 链路使用 startListening()+waitForConnection()，此方法为接口兼容保留 */
    override suspend fun connect() { }

    /** 断开当前 TCP 连接（不停止监听，可继续等待下一个连接） */
    override suspend fun disconnect() {
        if (!_isConnected) return
        _isConnected = false

        try { inputStream?.close() } catch (_: Exception) {}
        try { outputStream?.close() } catch (_: Exception) {}
        try { clientSocket?.close() } catch (_: Exception) {}
        inputStream = null
        outputStream = null
        clientSocket = null

        Log.i(TAG, "Client disconnected")
    }

    /** 停止监听（关闭 ServerSocket，释放所有资源） */
    fun stopListening() {
        _listening = false
        _isConnected = false
        scope?.cancel()
        scope = null
        try { serverSocket?.close() } catch (_: Exception) {}
        try { clientSocket?.close() } catch (_: Exception) {}
        serverSocket = null
        clientSocket = null
        Log.i(TAG, "TCP server stopped")
    }

    override suspend fun send(data: ByteArray) {
        sendBlocking(data)
    }

    /**
     * 阻塞发送（供非协程的音频采集线程使用）
     * 写入完整包字节数组到 TCP 流，synchronized 保证线程安全
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

    /** 是否正在监听 */
    val isListening: Boolean get() = _listening
}
