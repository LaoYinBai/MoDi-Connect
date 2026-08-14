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

import com.modi.connect.core.adapters.BluetoothTransport
import com.modi.protocol.PacketHeaderCodec
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.session.HandshakeResult
import com.modi.connect.session.HelloSessionPayload
import com.modi.protocol.Packet
import com.modi.protocol.LinkType
import com.modi.protocol.PacketType
import java.util.UUID
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.withTimeoutOrNull

/**
 * BtHandshakeClient — 蓝牙链路主动握手（Android 端）
 *
 * 职责：构造 HELLO(route+token) → 发送 → 等待 HELLO_ACK。
 * 与 WifiDirect 的 HandshakeManager.handshake() 对称。
 */
object BtHandshakeClient {

    private const val TAG = "BtHandshake"
    private const val BT_TOKEN = "MODI"  // 必须 ≤ 8 字符（payload 限制）
    private const val ACK_TIMEOUT_S = 10L
    private const val ACK_MAX_ATTEMPTS = 3

    /**
     * 发送 HELLO 并等待 ACK。
     * 流程：构造 payload[0]=route,[1-8]=token → 发送 → 注册回调等 ACK → 超时重试。
     * 阻塞调用，必须在 IO 线程执行。
     *
     * @return ACK 中的 route（0-3），失败返回 -1
     */
    suspend fun sendHelloAndWaitForAck(
        transport: BluetoothTransport,
        route: Int,
        sessionId: UUID
    ): HandshakeResult? {
        val protocol = PacketHeaderCodec()

        val payload = HelloSessionPayload.encode(route, BT_TOKEN, sessionId)

        val packet = Packet(PacketType.HELLO, LinkType.BLUETOOTH, 0u, payload)
        val encoded = protocol.encode(packet)

        try {
            for (attempt in 1..ACK_MAX_ATTEMPTS) {
                val result = CompletableDeferred<HandshakeResult?>()

                transport.onPacketReceived = { data ->
                    val decoded = protocol.decode(data)
                    if (decoded != null && decoded.type == PacketType.HELLO_ACK &&
                        HelloSessionPayload.matchesAck(decoded.payload, sessionId)
                    ) {
                        val identity = HelloSessionPayload.decode(decoded.payload, tokenRequired = false)!!
                        result.complete(HandshakeResult(identity.route, identity.sessionId))
                    } else if (decoded != null && decoded.type == PacketType.HELLO_NACK) {
                        result.complete(null)
                    }
                }

                transport.sendBlocking(encoded)
                Log.i(TAG, "HELLO sent (attempt $attempt/$ACK_MAX_ATTEMPTS)")

                val reply = withTimeoutOrNull(ACK_TIMEOUT_S * 1_000L) { result.await() }
                transport.onPacketReceived = null

                if (reply != null) {
                    Log.i(TAG, "HELLO_ACK received, route=${reply.route}")
                    return reply
                }
                Log.w(TAG, "ACK timeout (attempt $attempt)")
            }
        } finally {
            transport.onPacketReceived = null
        }

        return null
    }
}
