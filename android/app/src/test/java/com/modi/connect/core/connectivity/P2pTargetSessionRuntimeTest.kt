package com.modi.connect.core.connectivity

import com.modi.connect.core.connectivity.wifidirect.P2pTargetComposition
import com.modi.connect.core.connectivity.wifidirect.P2pTargetSessionRuntime
import com.modi.protocol.ITransport
import com.modi.protocol.TransportType
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

class P2pTargetSessionRuntimeTest {
    @Test
    fun `authenticated P2P session maps stable private peer and preserves audio bytes`() = runTest {
        val legacy = FakeTransport()
        var requestedHost: String? = null
        var requestedBindAddress: String? = null
        val runtime = P2pTargetSessionRuntime(
            sessionUuid = UUID.fromString("33333333-3333-3333-3333-333333333333"),
            peerCredential = "paired-token",
            host = "192.168.49.2",
            localBindAddress = "192.168.49.1",
            createTransport = { host, bindAddress ->
                requestedHost = host
                requestedBindAddress = bindAddress
                legacy
            },
        )

        runtime.connect()
        runtime.audioChannel.open()
        var received = byteArrayOf()
        runtime.audioChannel.onBytesReceived = { received = it }
        runtime.audioChannel.send(byteArrayOf(1, 2, 3))
        legacy.emit(byteArrayOf(4, 5, 6))
        runtime.observeStreaming()

        assertEquals("192.168.49.2", requestedHost)
        assertEquals("192.168.49.1", requestedBindAddress)
        assertEquals(
            "wifi-direct:44f9987a7df7f2aca03c23807f8f07e8f14d76471eeefc6b23fd24618cbf3aec",
            runtime.peer.id.value,
        )
        assertFalse(runtime.peer.id.value.contains("paired-token"))
        assertEquals(TransportKind.WifiDirect, runtime.session.transport)
        assertEquals(SessionState.Streaming, runtime.session.state)
        assertArrayEquals(byteArrayOf(1, 2, 3), legacy.lastSent)
        assertArrayEquals(byteArrayOf(4, 5, 6), received)
    }

    @Test
    fun `P2P target gate remains disabled unless build requests it`() {
        assertFalse(P2pTargetComposition.isEnabled(false))
        assertTrue(P2pTargetComposition.isEnabled(true))
    }

    @Test(expected = IllegalArgumentException::class)
    fun `P2P runtime rejects missing authorized peer credential`() {
        P2pTargetSessionRuntime(
            sessionUuid = UUID.fromString("44444444-4444-4444-4444-444444444444"),
            peerCredential = " ",
            host = "192.168.49.2",
            localBindAddress = "192.168.49.1",
            createTransport = { _, _ -> FakeTransport() },
        )
    }

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
