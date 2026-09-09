package com.modi.connect.core.connectivity

import com.modi.connect.core.connectivity.channel.LegacyAudioChannelAdapter
import com.modi.connect.core.connectivity.channel.AudioChannelComposition
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Test

class LegacyAudioChannelAdapterTest {
    @Test
    fun `adapter preserves wire bytes and can restart`() = runTest {
        val transport = FakeTransportSession()
        val channel = LegacyAudioChannelAdapter(SessionId.parse("session-a"), transport)
        var received = byteArrayOf()
        channel.onBytesReceived = { received = it }

        channel.open()
        channel.send(byteArrayOf(1, 2, 3))
        transport.emit(byteArrayOf(4, 5, 6))
        channel.close()
        channel.open()

        assertArrayEquals(byteArrayOf(1, 2, 3), transport.lastSent)
        assertArrayEquals(byteArrayOf(4, 5, 6), received)
        assertEquals(0, channel.reserveSequence())
        assertEquals(false, AudioChannelComposition.isEnabled { null })
    }

    private class FakeTransportSession : TransportSession {
        override val descriptor = TransportDescriptor.forKind(TransportKind.Lan)
        override val state = TransportSessionState.Connected
        override var onBytesReceived: ((ByteArray) -> Unit)? = null
        var lastSent = byteArrayOf()
        override suspend fun discover(): List<TransportCandidate> = emptyList()
        override suspend fun listen() = Unit
        override suspend fun connect(endpoint: TransportEndpoint) = Unit
        override suspend fun send(payload: ByteArray) { lastSent = payload.copyOf() }
        override suspend fun close() = Unit
        fun emit(payload: ByteArray) = onBytesReceived?.invoke(payload)
    }
}
