package com.modi.connect.core.connectivity

import com.modi.connect.core.connectivity.usb.UsbTargetComposition
import com.modi.connect.core.connectivity.usb.UsbTargetSessionRuntime
import com.modi.protocol.ITransport
import com.modi.protocol.TransportType
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

class UsbTargetSessionRuntimeTest {
    @Test
    fun `authorized ADB tunnel maps session and preserves audio bytes`() = runTest {
        val legacy = FakeTransport()
        val runtime = UsbTargetSessionRuntime(
            UUID.fromString("77777777-7777-7777-7777-777777777777"),
            legacy,
        )

        runtime.connect()
        runtime.audioChannel.open()
        var received = byteArrayOf()
        runtime.audioChannel.onBytesReceived = { received = it }
        runtime.audioChannel.send(byteArrayOf(1, 2, 3))
        legacy.emit(byteArrayOf(4, 5, 6))
        runtime.observeStreaming()

        assertEquals(TransportKind.Usb, runtime.session.transport)
        assertEquals(SessionState.Streaming, runtime.session.state)
        assertTrue(runtime.peer.id.value.startsWith("usb-session:"))
        assertArrayEquals(byteArrayOf(1, 2, 3), legacy.lastSent)
        assertArrayEquals(byteArrayOf(4, 5, 6), received)
    }

    @Test
    fun `USB target gate remains disabled unless build requests it`() {
        assertFalse(UsbTargetComposition.isEnabled(false))
        assertTrue(UsbTargetComposition.isEnabled(true))
    }

    private class FakeTransport : ITransport {
        override var onPacketReceived: ((ByteArray) -> Unit)? = null
        override val isConnected = true
        override val type = TransportType.Usb
        var lastSent = byteArrayOf()
        override suspend fun connect() = Unit
        override suspend fun disconnect() = Unit
        override suspend fun send(data: ByteArray) { lastSent = data.copyOf() }
        fun emit(data: ByteArray) = onPacketReceived?.invoke(data)
    }
}
