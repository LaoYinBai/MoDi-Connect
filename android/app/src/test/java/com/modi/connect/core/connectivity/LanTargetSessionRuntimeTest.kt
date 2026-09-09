package com.modi.connect.core.connectivity

import com.modi.connect.core.connectivity.lan.LanTargetComposition
import com.modi.connect.core.connectivity.lan.LanTargetSessionRuntime
import com.modi.protocol.ITransport
import com.modi.protocol.TransportType
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

class LanTargetSessionRuntimeTest {
    @Test
    fun `runtime maps peer session transport channel and preserves bytes`() = runTest {
        val legacy = FakeTransport()
        val runtime = LanTargetSessionRuntime(
            sessionUuid = UUID.fromString("11111111-1111-1111-1111-111111111111"),
            peerKey = "DESKTOP",
            host = "192.0.2.1",
            createTransport = { legacy },
        )
        runtime.connect()
        var received = byteArrayOf()
        runtime.audioChannel.onBytesReceived = { received = it }
        runtime.audioChannel.open()

        runtime.audioChannel.send(byteArrayOf(1, 2, 3))
        legacy.emit(byteArrayOf(4, 5, 6))
        runtime.observeStreaming()

        assertArrayEquals(byteArrayOf(1, 2, 3), legacy.lastSent)
        assertArrayEquals(byteArrayOf(4, 5, 6), received)
        assertEquals("lan:DESKTOP", runtime.peer.id.value)
        assertEquals(SessionState.Streaming, runtime.session.state)
        assertEquals(AudioChannel.primary, runtime.audioChannel.descriptor.id)
    }

    @Test
    fun `closing session closes channel and transport without changing another runtime`() = runTest {
        val firstTransport = FakeTransport()
        val secondTransport = FakeTransport()
        val first = runtime("11111111-1111-1111-1111-111111111111", firstTransport)
        val second = runtime("22222222-2222-2222-2222-222222222222", secondTransport)
        first.connect(); first.audioChannel.open()
        second.connect(); second.audioChannel.open()

        first.close()

        assertEquals(SessionState.Closed, first.session.state)
        assertEquals(ChannelRuntimeState.Closed, first.audioChannel.state)
        assertFalse(firstTransport.connected)
        assertTrue(secondTransport.connected)
        assertEquals(SessionState.Ready, second.session.state)
    }

    @Test
    fun `feature gate remains disabled unless build requests target LAN`() {
        assertFalse(LanTargetComposition.isEnabled(false))
        assertTrue(LanTargetComposition.isEnabled(true))
    }

    private fun runtime(id: String, transport: FakeTransport) = LanTargetSessionRuntime(
        sessionUuid = UUID.fromString(id),
        peerKey = "DESKTOP",
        host = "192.0.2.1",
        createTransport = { transport },
    )

    private class FakeTransport : ITransport {
        override var onPacketReceived: ((ByteArray) -> Unit)? = null
        override val isConnected: Boolean get() = connected
        override val type = TransportType.Udp
        var connected = false
        var lastSent = byteArrayOf()
        override suspend fun connect() { connected = true }
        override suspend fun disconnect() { connected = false }
        override suspend fun send(data: ByteArray) { lastSent = data.copyOf() }
        fun emit(data: ByteArray) = onPacketReceived?.invoke(data)
    }
}
