package com.modi.connect.core.connectivity

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class TransportRegistryTest {
    @Test
    fun `unknown transport is unavailable and optional failure is isolated`() = runTest {
        val registry = TransportRegistry()
        registry.register(TransportKind.Lan) { StubSession(TransportKind.Lan) }
        registry.register(TransportKind.WifiDirect) { error("platform unavailable") }

        assertFalse(registry.tryCreate(TransportKind.Usb).isSuccess)
        assertTrue(registry.tryCreate(TransportKind.WifiDirect).isFailure)
        val lan = registry.tryCreate(TransportKind.Lan).getOrThrow()
        assertEquals(TransportKind.Lan, lan.descriptor.kind)
    }

    @Test(expected = IllegalStateException::class)
    fun `duplicate registration is rejected`() {
        val registry = TransportRegistry()
        registry.register(TransportKind.Lan) { StubSession(TransportKind.Lan) }
        registry.register(TransportKind.Lan) { StubSession(TransportKind.Lan) }
    }

    private class StubSession(kind: TransportKind) : TransportSession {
        override val descriptor = TransportDescriptor.forKind(kind)
        override val state = TransportSessionState.Idle
        override var onBytesReceived: ((ByteArray) -> Unit)? = null
        override suspend fun discover(): List<TransportCandidate> = emptyList()
        override suspend fun listen() = Unit
        override suspend fun connect(endpoint: TransportEndpoint) = Unit
        override suspend fun send(payload: ByteArray) = Unit
        override suspend fun close() = Unit
    }
}
