package com.modi.connect.session

import com.modi.protocol.LinkType
import com.modi.protocol.Packet
import com.modi.protocol.PacketType
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.nio.charset.StandardCharsets
import java.util.UUID

enum class SessionControlAction(val code: Byte) {
    DISCONNECT_REQUEST(1),
    DISCONNECT_ACK(2);

    companion object {
        fun fromCode(code: Byte): SessionControlAction? = entries.firstOrNull { it.code == code }
    }
}

enum class DisconnectReason(val code: Byte) {
    USER_SWITCH(1),
    REPAIR(2),
    USER_STOP(3);

    companion object {
        fun fromCode(code: Byte): DisconnectReason? = entries.firstOrNull { it.code == code }
    }
}

enum class DisconnectResult(val code: Byte) {
    NONE(0),
    ACCEPTED(1),
    IGNORED(2);

    companion object {
        fun fromCode(code: Byte): DisconnectResult? = entries.firstOrNull { it.code == code }
    }
}

data class SessionControlMessage(
    val action: SessionControlAction,
    val sessionId: UUID,
    val oldLink: Byte,
    val targetLink: Byte,
    val reason: DisconnectReason,
    val result: DisconnectResult
) {
    fun encode(): ByteArray = ByteArray(MESSAGE_LENGTH).also { payload ->
        payload[0] = DOMAIN
        payload[1] = MESSAGE_TYPE
        payload[2] = VERSION
        payload[3] = action.code
        sessionId.writeNetworkBytes(payload, SESSION_OFFSET)
        payload[20] = oldLink
        payload[21] = targetLink
        payload[22] = reason.code
        payload[23] = result.code
    }

    fun toPacket(): Packet = Packet(PacketType.DATA, oldLink, 0u, encode())

    companion object {
        private const val MESSAGE_LENGTH = 24
        private const val SESSION_OFFSET = 4
        private const val DOMAIN: Byte = 0x0f
        private const val MESSAGE_TYPE: Byte = 0x01
        private const val VERSION: Byte = 0x01

        fun request(
            sessionId: UUID,
            oldLink: Byte,
            targetLink: Byte,
            reason: DisconnectReason
        ): SessionControlMessage {
            require(isLinkType(oldLink) && isLinkType(targetLink)) { "Unsupported link type" }
            return SessionControlMessage(
                SessionControlAction.DISCONNECT_REQUEST,
                sessionId,
                oldLink,
                targetLink,
                reason,
                DisconnectResult.NONE
            )
        }

        fun ack(request: SessionControlMessage, result: DisconnectResult): SessionControlMessage {
            require(request.action == SessionControlAction.DISCONNECT_REQUEST) { "ACK requires a request" }
            require(result != DisconnectResult.NONE) { "ACK result must be accepted or ignored" }
            return request.copy(action = SessionControlAction.DISCONNECT_ACK, result = result)
        }

        fun decode(payload: ByteArray): SessionControlMessage? {
            if (payload.size != MESSAGE_LENGTH ||
                payload[0] != DOMAIN || payload[1] != MESSAGE_TYPE || payload[2] != VERSION
            ) return null

            val action = SessionControlAction.fromCode(payload[3]) ?: return null
            val oldLink = payload[20]
            val targetLink = payload[21]
            if (!isLinkType(oldLink) || !isLinkType(targetLink)) return null
            val reason = DisconnectReason.fromCode(payload[22]) ?: return null
            val result = DisconnectResult.fromCode(payload[23]) ?: return null
            if (action == SessionControlAction.DISCONNECT_REQUEST && result != DisconnectResult.NONE) return null
            if (action == SessionControlAction.DISCONNECT_ACK && result == DisconnectResult.NONE) return null

            return SessionControlMessage(
                action,
                uuidFromNetworkBytes(payload, SESSION_OFFSET),
                oldLink,
                targetLink,
                reason,
                result
            )
        }
    }
}

data class HelloSessionIdentity(
    val route: Int,
    val token: String?,
    val sessionId: UUID
)

data class HandshakeResult(val route: Int, val sessionId: UUID)

data class P2pHandshakeResult(val remoteIp: String, val sessionId: UUID)

object HelloSessionPayload {
    private const val TOKEN_LENGTH = 8
    private const val SESSION_LENGTH = 16

    fun encode(route: Int, token: String?, sessionId: UUID): ByteArray {
        require(route in 0..3) { "Route must be between 0 and 3" }
        val tokenBytes = token?.toByteArray(StandardCharsets.US_ASCII)
        require(tokenBytes == null || tokenBytes.isNotEmpty() && tokenBytes.size <= TOKEN_LENGTH) {
            "Token must contain 1 to 8 ASCII bytes"
        }
        require(token == null || tokenBytes!!.toString(StandardCharsets.US_ASCII) == token) {
            "Token must contain ASCII characters only"
        }

        val sessionOffset = 1 + (if (tokenBytes == null) 0 else TOKEN_LENGTH)
        return ByteArray(sessionOffset + SESSION_LENGTH).also { payload ->
            payload[0] = route.toByte()
            tokenBytes?.copyInto(payload, destinationOffset = 1)
            sessionId.writeNetworkBytes(payload, sessionOffset)
        }
    }

    fun decode(payload: ByteArray, tokenRequired: Boolean): HelloSessionIdentity? {
        val sessionOffset = if (tokenRequired) 1 + TOKEN_LENGTH else 1
        if (payload.size != sessionOffset + SESSION_LENGTH) return null
        val route = payload[0].toInt()
        if (route !in 0..3) return null

        val token = if (tokenRequired) {
            val tokenEnd = (1 until sessionOffset).firstOrNull { payload[it] == 0.toByte() } ?: sessionOffset
            if ((tokenEnd until sessionOffset).any { payload[it] != 0.toByte() }) return null
            if (tokenEnd == 1) return null
            val decoded = payload.copyOfRange(1, tokenEnd).toString(StandardCharsets.US_ASCII)
            if (!decoded.all { it.code in 0x20..0x7e }) return null
            decoded
        } else {
            null
        }

        return HelloSessionIdentity(route, token, uuidFromNetworkBytes(payload, sessionOffset))
    }

    fun matchesAck(payload: ByteArray, expectedSessionId: UUID): Boolean =
        decode(payload, tokenRequired = false)?.sessionId == expectedSessionId
}

private fun isLinkType(linkType: Byte): Boolean = when (linkType) {
    LinkType.WIFI_LAN, LinkType.WIFI_DIRECT, LinkType.BLUETOOTH, LinkType.USB -> true
    else -> false
}

private fun UUID.writeNetworkBytes(destination: ByteArray, offset: Int) {
    ByteBuffer.wrap(destination, offset, 16)
        .order(ByteOrder.BIG_ENDIAN)
        .putLong(mostSignificantBits)
        .putLong(leastSignificantBits)
}

private fun uuidFromNetworkBytes(source: ByteArray, offset: Int): UUID {
    val buffer = ByteBuffer.wrap(source, offset, 16).order(ByteOrder.BIG_ENDIAN)
    return UUID(buffer.long, buffer.long)
}
