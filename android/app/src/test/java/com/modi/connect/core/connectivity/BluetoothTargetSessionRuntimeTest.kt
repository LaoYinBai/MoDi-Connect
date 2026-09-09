package com.modi.connect.core.connectivity

import com.modi.connect.core.connectivity.bluetooth.BluetoothTargetComposition
import com.modi.connect.core.connectivity.bluetooth.BluetoothTargetSessionRuntime
import com.modi.protocol.ITransport
import com.modi.protocol.TransportType
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

class BluetoothTargetSessionRuntimeTest {
    @Test
    fun `bonded Bluetooth target maps private peer and preserves audio bytes`() = runTest {
        val legacy = FakeTransport()
        val runtime = BluetoothTargetSessionRuntime(
            sessionUuid = UUID.fromString("55555555-5555-5555-5555-555555555555"),
            peerCredential = "AA:BB:CC:DD:EE:FF",
            legacy = legacy,
        )

        runtime.connect()
        runtime.audioChannel.open()
        var received = byteArrayOf()
        runtime.audioChannel.onBytesReceived = { received = it }
        runtime.audioChannel.send(byteArrayOf(1, 2, 3))
        legacy.emit(byteArrayOf(4, 5, 6))
        runtime.observeStreaming()

        assertEquals(TransportKind.Bluetooth, runtime.session.transport)
        assertEquals(SessionState.Streaming, runtime.session.state)
        assertTrue(runtime.peer.id.value.startsWith("bluetooth:"))
        assertFalse(runtime.peer.id.value.contains("AA:BB"))
        assertArrayEquals(byteArrayOf(1, 2, 3), legacy.lastSent)
        assertArrayEquals(byteArrayOf(4, 5, 6), received)
    }

    @Test(expected = IllegalArgumentException::class)
    fun `Bluetooth target rejects missing bonded peer identity`() {
        BluetoothTargetSessionRuntime(
            UUID.fromString("66666666-6666-6666-6666-666666666666"),
            " ",
            FakeTransport(),
        )
    }

    @Test
    fun `Bluetooth target gate remains disabled unless build requests it`() {
        assertFalse(BluetoothTargetComposition.isEnabled(false))
        assertTrue(BluetoothTargetComposition.isEnabled(true))
    }

    private class FakeTransport : ITransport {
        override var onPacketReceived: ((ByteArray) -> Unit)? = null
        override val isConnected = true
        override val type = TransportType.Bluetooth
        var lastSent = byteArrayOf()
        override suspend fun connect() = Unit
        override suspend fun disconnect() = Unit
        override suspend fun send(data: ByteArray) { lastSent = data.copyOf() }
        fun emit(data: ByteArray) = onPacketReceived?.invoke(data)
    }
}
