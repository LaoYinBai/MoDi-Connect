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
package com.modi.connect.net

import com.modi.protocol.LinkType
import com.modi.protocol.PacketType
import com.modi.protocol.IPacketProtocol
import com.modi.protocol.PacketHeader

import com.modi.connect.core.adapters.UdpTransport
import com.modi.connect.core.TransportIdentity
import com.modi.protocol.TransportType
import com.modi.connect.core.factory.PlatformFactory
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.session.HelloSessionPayload
import com.modi.connect.session.SessionControlMessage
import com.modi.protocol.Packet
import java.util.UUID
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull

/**
 * HandshakeManager — 共享主动握手工具（无状态）
 *
 * ## 职责
 * 1. 发 HELLO 到电脑 12347 端口，等 HELLO_ACK（LAN / P2P 共用）
 * 2. 推流中发 ROUTE 热切路线
 *
 * ## 约束
 * - 无状态：不持有任何链路状态，localBindAddress 由调用方传入
 * - 每次握手/热切独立创建和释放 UdpTransport
 * - 所有异常统一捕获返回 false
 */
object HandshakeManager {

    private const val TAG = "HandshakeManager"
    private const val HANDSHAKE_PORT = TransportIdentity.HANDSHAKE_PORT
    private const val TIMEOUT_MS = 500L       // LAN 模式超时
    private const val P2P_TIMEOUT_MS = 3000L  // P2P 模式超时（链路延迟较高）
    private val protocol = PlatformFactory.createProtocol()

    /**
     * 握手 — 发 HELLO 到电脑，等 HELLO_ACK
     *
     * @param host 目标 IP
     * @param route 路线编号 0~3
     * @param token 认证 token（LAN 模式传 null）
     * @param linkType 链路类型
     * @param localBindAddress 本地绑定地址（P2P 模式传 P2P 接口 IP，LAN 传 null）
     * @return true 表示握手成功（收到 HELLO_ACK）
     */
    suspend fun handshake(
        host: String,
        route: Int,
        token: String? = null,
        linkType: Byte = LinkType.WIFI_LAN,
        localBindAddress: String? = null,
        sessionId: UUID
    ): Boolean {
        return withContext(Dispatchers.IO) {
            tryHandshake(host, route, token, linkType, localBindAddress, sessionId)
        }
    }

    /**
     * 单次握手尝试 — 发 HELLO 到指定 IP，等 HELLO_ACK
     * P2P 模式下重发 3 次（ARP 解析可能丢弃第一包）
     */
    private suspend fun tryHandshake(
        host: String,
        route: Int,
        token: String?,
        linkType: Byte,
        localBindAddress: String?,
        sessionId: UUID
    ): Boolean {
        var transport: UdpTransport? = null
        return try {
            Log.i(TAG, "tryHandshake: host=$host, route=$route, token=${token?.take(4)}..., linkType=$linkType, bind=$localBindAddress")
            transport = PlatformFactory.createTransport(
                type = TransportType.Udp, host = host, port = HANDSHAKE_PORT,
                localBindAddress = localBindAddress
            ) as UdpTransport
            val reply = CompletableDeferred<ByteArray?>()
            transport.onPacketReceived = { data -> reply.complete(data) }
            transport.connect()
            Log.i(TAG, "tryHandshake: socket connected to $host:$HANDSHAKE_PORT")

            val payload = HelloSessionPayload.encode(route, token, sessionId)
            val helloPacket = Packet(PacketType.HELLO, linkType, 0u, payload)
            val encoded = protocol.encode(helloPacket)

            // 重发 3 次（间隔 800ms）：冷启动时 Windows 端可能尚未就绪，ARP 解析也可能丢弃前几包
            val attempts = 3
            val timeout = if (localBindAddress != null) P2P_TIMEOUT_MS else TIMEOUT_MS

            for (i in 1..attempts) {
                transport.sendBlocking(encoded)
                Log.i(TAG, "tryHandshake: HELLO sent attempt $i/$attempts (${payload.size}B)")

                val waitMs = if (i < attempts) 800L else timeout
                val response = withTimeoutOrNull(waitMs) { reply.await() }
                if (response != null) {
                    val decoded = protocol.decode(response)
                    Log.i(TAG, "tryHandshake: got reply, type=${decoded?.type}")
                    return decoded != null &&
                        decoded.type == PacketType.HELLO_ACK &&
                        HelloSessionPayload.matchesAck(decoded.payload, sessionId)
                }
                if (i < attempts) Log.i(TAG, "tryHandshake: no reply, retrying...")
            }

            Log.w(TAG, "tryHandshake: FAILED after $attempts attempts, no HELLO_ACK from $host:$HANDSHAKE_PORT")
            false
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (e: Exception) {
            Log.e(TAG, "tryHandshake error: ${e.message}")
            false
        } finally {
            withContext(NonCancellable) { transport?.disconnect() }
        }
    }

    /**
     * 推流中切换路线 — 发 ROUTE 通知电脑
     *
     * @param host 目标 IP
     * @param route 路线编号 0~3
     * @param linkType 链路类型
     * @param localBindAddress 本地绑定地址（P2P 模式传 P2P 接口 IP，LAN 传 null）
     */
    suspend fun sendRouteUpdate(host: String, route: Int, linkType: Byte = LinkType.WIFI_LAN, localBindAddress: String? = null) {
        var transport: UdpTransport? = null
        try {
            transport = PlatformFactory.createTransport(
                type = TransportType.Udp, host = host, port = HANDSHAKE_PORT,
                localBindAddress = localBindAddress
            ) as UdpTransport
            transport.connect()

            // 编码并发送 ROUTE 包
            val routePacket = Packet(PacketType.ROUTE, linkType, 0u, byteArrayOf(route.toByte()))
            transport.sendBlocking(protocol.encode(routePacket))
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (e: Exception) {
            Log.e(TAG, "sendRouteUpdate error: ${e.message}")
        } finally {
            withContext(NonCancellable) { transport?.disconnect() }
        }
    }

    suspend fun sendSessionControl(
        host: String,
        message: SessionControlMessage,
        localBindAddress: String? = null
    ): Boolean {
        var transport: UdpTransport? = null
        return try {
            transport = PlatformFactory.createTransport(
                type = TransportType.Udp,
                host = host,
                port = HANDSHAKE_PORT,
                localBindAddress = localBindAddress
            ) as UdpTransport
            transport.connect()
            transport.sendBlocking(protocol.encode(message.toPacket()))
            true
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (e: Exception) {
            Log.e(TAG, "sendSessionControl error: ${e.message}")
            false
        } finally {
            withContext(NonCancellable) { transport?.disconnect() }
        }
    }
}
