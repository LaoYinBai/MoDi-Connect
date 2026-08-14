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
import kotlinx.coroutines.*
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress

/**
 * UdpTransport — ITransport 的 UDP 实现
 *
 * 包裹 DatagramSocket，支持 server 模式（仅绑定端口）和 client 模式（指定远程主机）。
 * client 模式绑定随机端口（port=0），server 模式绑定指定端口。
 */
class UdpTransport(
    private val localPort: Int,
    private val remoteHost: String? = null,
    private val remotePort: Int? = null,
    private val localBindAddress: String? = null  // P2P 模式：绑定到 P2P 接口本地 IP
) : ITransport {
    // client 模式绑定随机端口（可指定本地地址），server 模式绑定指定端口
    private val socket: DatagramSocket = when {
        remoteHost != null && localBindAddress != null ->
            DatagramSocket(0, InetAddress.getByName(localBindAddress))
        remoteHost != null -> DatagramSocket()
        else -> DatagramSocket(localPort)
    }
    private var _isConnected = false
    private var scope: CoroutineScope? = null
    private val recvBuf = ByteArray(65507) // 最大 UDP 包

    /** 服务端模式：最后收到包的远端 IP */
    var lastRemoteHost: String? = null
        private set

    /** 服务端模式：最后收到包的远端端口（用于精确回送 ACK） */
    var lastRemotePort: Int = 0
        private set

    override suspend fun connect() {
        if (_isConnected) return

        if (remoteHost != null) {
            socket.connect(InetSocketAddress(remoteHost, remotePort ?: 12345))
        }
        _isConnected = true
        scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        scope!!.launch { receiveLoop() }
    }

    override suspend fun disconnect() {
        _isConnected = false
        scope?.cancel()
        scope = null
        if (!socket.isClosed) socket.close()  // 关闭 socket 释放端口
    }

    override suspend fun send(data: ByteArray) {
        try {
            if (remoteHost != null) {
                // client 模式：connect 后直接 send
                socket.send(DatagramPacket(data, data.size))
            } else {
                Log.w("UdpTransport", "send called in server mode without remote address")
            }
        } catch (e: Exception) {
            Log.e("UdpTransport", "send error: ${e.message}")
        }
    }

    /**
     * 阻塞发送（供非协程的音频采集线程使用，不走 suspend）
     * 仅在 client 模式（已 connect）下有效
     */
    fun sendBlocking(data: ByteArray) {
        try {
            socket.send(DatagramPacket(data, data.size))
        } catch (e: Exception) {
            Log.e("UdpTransport", "sendBlocking error: ${e.message}")
        }
    }

    /**
     * 服务端模式：发送数据到指定远端地址
     */
    fun sendTo(data: ByteArray, host: String, port: Int) {
        try {
            val packet = DatagramPacket(data, data.size, InetAddress.getByName(host), port)
            socket.send(packet)
        } catch (e: Exception) {
            Log.e("UdpTransport", "sendTo error: ${e.message}")
        }
    }

    private suspend fun receiveLoop() = withContext(Dispatchers.IO) {
        while (isActive && _isConnected) {
            try {
                val packet = DatagramPacket(recvBuf, recvBuf.size)
                socket.receive(packet)
                lastRemoteHost = packet.address.hostAddress  // 记录远端 IP
                lastRemotePort = packet.port                  // 记录远端端口
                val bytes = packet.data.copyOfRange(0, packet.length)
                onPacketReceived?.invoke(bytes)
            } catch (e: CancellationException) { break }
            catch (e: Exception) {
                Log.e("UdpTransport", "receiveLoop error: ${e.message}")
            }
        }
    }

    override var onPacketReceived: ((ByteArray) -> Unit)? = null
    override val isConnected: Boolean get() = _isConnected
    override val type: TransportType = TransportType.Udp
}
