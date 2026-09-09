package com.modi.connect.core.connectivity

import com.modi.connect.core.connectivity.transport.LegacyTransportSessionAdapter
import com.modi.protocol.ITransport
import com.modi.protocol.TransportType
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Test

class LegacyTransportSessionAdapterTest {
    @Test
    fun `adapter preserves bytes and owns connect send close`() = runTest {
        val legacy = FakeTransport()
        val session = LegacyTransportSessionAdapter(TransportDescriptor.forKind(TransportKind.Lan), legacy)
        var received = byteArrayOf()
        session.onBytesReceived = { received = it }

        session.connect(TransportEndpoint.parse("opaque-lan-endpoint"))
        session.send(byteArrayOf(1, 2, 3))
        legacy.emit(byteArrayOf(4, 5, 6))
        session.close()

        assertEquals(1, legacy.connectCalls)
        assertArrayEquals(byteArrayOf(1, 2, 3), legacy.lastSent)
        assertArrayEquals(byteArrayOf(4, 5, 6), received)
        assertEquals(1, legacy.disconnectCalls)
        assertEquals(TransportSessionState.Closed, session.state)
    }

    private class FakeTransport : ITransport {
        override var onPacketReceived: ((ByteArray) -> Unit)? = null
        override val isConnected: Boolean get() = connected
        override val type: TransportType = TransportType.Udp
        var connected = false
        var connectCalls = 0
        var disconnectCalls = 0
        var lastSent = byteArrayOf()
        override suspend fun connect() { connectCalls++; connected = true }
        override suspend fun disconnect() { disconnectCalls++; connected = false }
        override suspend fun send(data: ByteArray) { lastSent = data.copyOf() }
        fun emit(data: ByteArray) = onPacketReceived?.invoke(data)
    }
}
