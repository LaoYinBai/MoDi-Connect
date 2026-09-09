package com.modi.connect.core.connectivity

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Test

class DevMultiSessionCoordinatorTest {
    @Test
    fun `default disabled coordinator rejects sessions without touching resources`() = runTest {
        val coordinator = DevMultiSessionCoordinator()
        val transport = MemoryTransportSession()
        val channel = MemoryChannelDataPlane(SessionId.parse("phone-a"))

        runCatching { coordinator.start(SessionId.parse("phone-a"), transport, channel) }
            .onSuccess { error("Disabled coordinator accepted a session") }

        assertTrue(coordinator.activeSessionIds.isEmpty())
        assertEquals(0, transport.closeCount)
        assertEquals(0, channel.openCount)
    }

    @Test
    fun `independent sessions keep state sequence and resources isolated`() = runTest {
        val coordinator = DevMultiSessionCoordinator(enabled = true)
        val sessions = listOf("phone-a", "phone-b", "phone-c").map(::SessionFixture)

        sessions.forEach { coordinator.start(it.id, it.transport, it.channel) }

        assertEquals(3, coordinator.activeSessionIds.size)
        sessions.forEach { fixture ->
            assertEquals(0L, fixture.channel.reserveSequence())
            assertEquals(1L, fixture.channel.reserveSequence())
            assertSame(fixture.channel, coordinator.channels.find(fixture.id, AudioChannel.primary))
        }

        assertTrue(coordinator.observeState(sessions.first().id, SessionState.Reconnecting).applied)
        assertEquals(
            SessionState.Reconnecting,
            coordinator.sessions.snapshots.single { it.id == sessions.first().id }.state
        )
        coordinator.sessions.snapshots.filter { it.id != sessions.first().id }
            .forEach { assertEquals(SessionState.Ready, it.state) }

        coordinator.closeSession(sessions.first().id)

        assertEquals(2, coordinator.activeSessionIds.size)
        assertEquals(1, sessions.first().transport.closeCount)
        assertEquals(1, sessions.first().channel.closeCount)
        sessions.drop(1).forEach {
            assertEquals(0, it.transport.closeCount)
            assertEquals(ChannelRuntimeState.Open, it.channel.state)
        }

        coordinator.closeAll()

        assertTrue(coordinator.activeSessionIds.isEmpty())
        sessions.forEach {
            assertEquals(1, it.transport.closeCount)
            assertEquals(1, it.channel.closeCount)
        }
        coordinator.sessions.snapshots.forEach { assertEquals(SessionState.Closed, it.state) }
    }

    private class SessionFixture(value: String) {
        val id = SessionId.parse(value)
        val transport = MemoryTransportSession()
        val channel = MemoryChannelDataPlane(id)
    }

    private class MemoryTransportSession : TransportSession {
        override val descriptor = TransportDescriptor.forKind(TransportKind.Lan)
        override var state = TransportSessionState.Connected
        override var onBytesReceived: ((ByteArray) -> Unit)? = null
        var closeCount = 0
        override suspend fun discover() = emptyList<TransportCandidate>()
        override suspend fun listen() = Unit
        override suspend fun connect(endpoint: TransportEndpoint) = Unit
        override suspend fun send(payload: ByteArray) = Unit
        override suspend fun close() {
            closeCount++
            state = TransportSessionState.Closed
        }
    }

    private class MemoryChannelDataPlane(override val sessionId: SessionId) : ChannelDataPlane {
        private var next = 0L
        override val descriptor = AudioChannel.descriptor
        override var state = ChannelRuntimeState.Closed
        override val nextSequence get() = next
        override var onBytesReceived: ((ByteArray) -> Unit)? = null
        var openCount = 0
        var closeCount = 0
        override suspend fun open() {
            openCount++
            state = ChannelRuntimeState.Open
            next = 0
        }
        override fun reserveSequence() = next++
        override fun resetSequence() { next = 0 }
        override suspend fun send(payload: ByteArray) = Unit
        override suspend fun close() {
            closeCount++
            state = ChannelRuntimeState.Closed
        }
    }
}
