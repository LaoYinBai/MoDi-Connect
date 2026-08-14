package com.modi.connect.session

import com.modi.protocol.LinkType
import com.modi.protocol.PacketType
import java.util.UUID
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SessionControlMessageTest {
    private val sessionId = UUID.fromString("00112233-4455-6677-8899-aabbccddeeff")

    @Test
    fun `disconnect request uses stable cross platform bytes`() {
        val message = SessionControlMessage.request(
            sessionId,
            oldLink = LinkType.WIFI_LAN,
            targetLink = LinkType.BLUETOOTH,
            reason = DisconnectReason.USER_SWITCH
        )

        assertArrayEquals(
            hex("0F01010100112233445566778899AABBCCDDEEFF01030100"),
            message.encode()
        )
        assertEquals(PacketType.DATA, message.toPacket().type)
        assertEquals(LinkType.WIFI_LAN, message.toPacket().linkType)
    }

    @Test
    fun `ack preserves the request session identity`() {
        val request = SessionControlMessage.request(
            sessionId,
            LinkType.WIFI_DIRECT,
            LinkType.USB,
            DisconnectReason.REPAIR
        )

        val ack = SessionControlMessage.ack(request, DisconnectResult.ACCEPTED)

        assertEquals(SessionControlAction.DISCONNECT_ACK, ack.action)
        assertEquals(sessionId, ack.sessionId)
        assertEquals(LinkType.WIFI_DIRECT, ack.oldLink)
        assertEquals(LinkType.USB, ack.targetLink)
        assertEquals(DisconnectResult.ACCEPTED, ack.result)
        assertEquals(ack, SessionControlMessage.decode(ack.encode()))
    }

    @Test
    fun `malformed disconnect messages are rejected`() {
        val valid = hex("0F01010100112233445566778899AABBCCDDEEFF01030100")

        assertNull(SessionControlMessage.decode(valid.copyOf(23)))
        assertNull(SessionControlMessage.decode(valid.copyOf().also { it[0] = 0x0e }))
        assertNull(SessionControlMessage.decode(valid.copyOf().also { it[2] = 0x02 }))
        assertNull(SessionControlMessage.decode(valid.copyOf().also { it[3] = 0x7f }))
        assertNull(SessionControlMessage.decode(valid.copyOf().also { it[20] = 0x7f }))
        assertNull(SessionControlMessage.decode(valid.copyOf().also { it[21] = 0x7f }))
    }

    @Test
    fun `hello payload keeps route token and RFC 4122 session bytes`() {
        assertArrayEquals(
            hex("0200112233445566778899AABBCCDDEEFF"),
            HelloSessionPayload.encode(route = 2, token = null, sessionId = sessionId)
        )
        assertArrayEquals(
            hex("024D4F44490000000000112233445566778899AABBCCDDEEFF"),
            HelloSessionPayload.encode(route = 2, token = "MODI", sessionId = sessionId)
        )

        assertEquals(
            HelloSessionIdentity(2, null, sessionId),
            HelloSessionPayload.decode(
                hex("0200112233445566778899AABBCCDDEEFF"),
                tokenRequired = false
            )
        )
        assertEquals(
            HelloSessionIdentity(2, "MODI", sessionId),
            HelloSessionPayload.decode(
                hex("024D4F44490000000000112233445566778899AABBCCDDEEFF"),
                tokenRequired = true
            )
        )
        assertNull(HelloSessionPayload.decode(byteArrayOf(4), tokenRequired = false))
        assertNull(HelloSessionPayload.decode(ByteArray(25), tokenRequired = true))
    }

    @Test
    fun `hello ack must echo the expected session identity`() {
        val ack = HelloSessionPayload.encode(route = 2, token = null, sessionId = sessionId)

        assertTrue(HelloSessionPayload.matchesAck(ack, sessionId))
        assertFalse(HelloSessionPayload.matchesAck(ack, UUID.randomUUID()))
        assertFalse(HelloSessionPayload.matchesAck(byteArrayOf(2), sessionId))
    }

    private fun hex(value: String): ByteArray =
        value.chunked(2).map { it.toInt(16).toByte() }.toByteArray()
}
