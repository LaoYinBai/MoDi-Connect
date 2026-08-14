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

import com.modi.protocol.IPacketProtocol
import com.modi.protocol.PacketHeader

import com.modi.connect.core.adapters.UdpTransport
import com.modi.protocol.TransportType
import com.modi.connect.core.factory.PlatformFactory
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.session.HelloSessionPayload
import com.modi.connect.session.P2pHandshakeResult
import com.modi.protocol.Packet
import com.modi.protocol.LinkType
import com.modi.protocol.PacketType
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.withTimeoutOrNull

/**
 * P2pHandshakeServer — P2P 专属被动握手（Android 做 GO，等 Windows 发 HELLO）
 *
 * 职责单一：监听 UDP 12347，收到 HELLO 后校验 token，回复 HELLO_ACK。
 * 无状态：不持有任何链路状态，结果通过返回值传递。
 */
object P2pHandshakeServer {

    private const val TAG = "P2pHandshakeServer"
    private const val HANDSHAKE_PORT = 12347
    private val protocol = PlatformFactory.createProtocol()

    /**
     * 等待 Windows 发来的 HELLO（被动握手，Android 做 GO 监听 12347 端口）
     * 流程：创建 UDP Transport 监听 → 等待收包 → 解码校验 HELLO + token → 回 HELLO_ACK 到来源端口
     *
     * @param expectedToken 期望的 token（从 QR 码 / 配对存储）
     * @param route 当前路线（0-3），通过 HELLO_ACK 告知 Windows
     * @return 成功时返回 Windows 远端 IP，失败返回 null
     */
    suspend fun waitForHello(expectedToken: String?, route: Int = 0): P2pHandshakeResult? {
        var transport: UdpTransport? = null
        return try {
            Log.i(TAG, "waitForHello: listening on 0.0.0.0:$HANDSHAKE_PORT, token=${expectedToken?.take(4)}...")
            transport = PlatformFactory.createTransport(
                type = TransportType.Udp, port = HANDSHAKE_PORT
            ) as UdpTransport
            val helloReceived = CompletableDeferred<Triple<ByteArray, String, Int>>()

            transport.onPacketReceived = { data ->
                val remoteIp = transport.lastRemoteHost ?: "unknown"
                val remotePort = transport.lastRemotePort
                helloReceived.complete(Triple(data, remoteIp, remotePort))
            }
            transport.connect()

            // 等待 HELLO（最多 60s）
            val result = withTimeoutOrNull(60_000L) { helloReceived.await() }
            if (result == null) {
                Log.w(TAG, "waitForHello: TIMEOUT 60s, no HELLO received")
                return null
            }

            val (data, remoteIp, remotePort) = result
            Log.i(TAG, "waitForHello: received packet from $remoteIp:$remotePort, decoding...")

            val decoded = protocol.decode(data)
            if (decoded == null || decoded.type != PacketType.HELLO) {
                Log.w(TAG, "waitForHello: not a HELLO packet, type=${decoded?.type}")
                return null
            }

            val identity = HelloSessionPayload.decode(decoded.payload, tokenRequired = true)
            if (identity == null || expectedToken != null && identity.token != expectedToken) {
                Log.w(TAG, "waitForHello: invalid session payload or token mismatch")
                return null
            }

            // 回复 HELLO_ACK 到来源端口
            val ackPayload = HelloSessionPayload.encode(route, null, identity.sessionId)
            val ackPacket = Packet(PacketType.HELLO_ACK, LinkType.WIFI_DIRECT, 0u, ackPayload)
            transport.sendTo(protocol.encode(ackPacket), remoteIp, remotePort)
            Log.i(TAG, "waitForHello: HELLO_ACK sent to $remoteIp:$remotePort (route=$route), handshake OK")
            P2pHandshakeResult(remoteIp, identity.sessionId)
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (e: Exception) {
            Log.e(TAG, "waitForHello error: ${e.message}")
            null
        } finally {
            transport?.disconnect()
        }
    }
}
