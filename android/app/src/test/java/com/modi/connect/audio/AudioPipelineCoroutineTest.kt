package com.modi.connect.audio

import com.modi.protocol.ITransport
import com.modi.protocol.TransportType
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AudioPipelineCoroutineTest {
    @Test
    fun connect_runs_on_injected_dispatcher() = runTest {
        val transport = FakeTransport()
        val connector = TransportConnector(StandardTestDispatcher(testScheduler))

        val job = launch { connector.connect(transport).getOrThrow() }
        assertFalse(transport.connected)
        testScheduler.advanceUntilIdle()

        assertTrue(transport.connected)
        job.join()
    }

    @Test
    fun cancellation_disconnects_partially_connected_transport() = runTest {
        val transport = FakeTransport(blockConnect = true)
        val connector = TransportConnector(StandardTestDispatcher(testScheduler))
        val job = launch { connector.connect(transport).getOrThrow() }
        testScheduler.runCurrent()
        transport.entered.await()

        job.cancel()
        testScheduler.advanceUntilIdle()

        assertEquals(1, transport.disconnectCalls)
    }

    private class FakeTransport(private val blockConnect: Boolean = false) : ITransport {
        override var onPacketReceived: ((ByteArray) -> Unit)? = null
        override val isConnected: Boolean get() = connected
        override val type: TransportType = TransportType.Udp
        var connected = false
        var disconnectCalls = 0
        val entered = CompletableDeferred<Unit>()

        override suspend fun connect() {
            connected = true
            entered.complete(Unit)
            if (blockConnect) CompletableDeferred<Unit>().await()
        }

        override suspend fun disconnect() { disconnectCalls++ }
        override suspend fun send(data: ByteArray) = Unit
    }
}
