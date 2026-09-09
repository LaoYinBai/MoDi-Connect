package com.modi.connect.core.connectivity

import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/**
 * Development-only isolation harness. It is not wired into production composition and accepts
 * only independently owned transports; it does not multiplex the approved wire format.
 */
class DevMultiSessionCoordinator(private val enabled: Boolean = false) {
    private val lock = Mutex()
    private val active = linkedMapOf<SessionId, TransportSession>()

    val sessions = SessionRegistry()
    val channels = ChannelRouter()
    val activeSessionIds: Set<SessionId> get() = synchronized(active) { active.keys.toSet() }

    suspend fun start(sessionId: SessionId, transport: TransportSession, channel: ChannelDataPlane) {
        check(enabled) { "The multi-session isolation lab is disabled" }
        require(channel.sessionId == sessionId) { "Channel session does not match the requested session" }

        lock.withLock {
            check(synchronized(active) { sessionId !in active }) {
                "Session is already active in the isolation lab"
            }
            channels.register(channel)
            try {
                channel.open()
                val observed = sessions.observeStarted(sessionId, transport.descriptor.kind, SessionState.Ready)
                check(observed.applied) { observed.reason ?: "Session could not be observed" }
                synchronized(active) { active[sessionId] = transport }
            } catch (error: Throwable) {
                channels.closeSession(sessionId)
                throw error
            }
        }
    }

    fun observeState(sessionId: SessionId, state: SessionState): SessionObservationResult =
        sessions.observeState(sessionId, state)

    suspend fun closeSession(sessionId: SessionId) {
        lock.withLock {
            val transport = synchronized(active) { active.remove(sessionId) } ?: return
            sessions.observeState(sessionId, SessionState.Closing)
            channels.closeSession(sessionId)
            transport.close()
            sessions.observeEnded(sessionId)
        }
    }

    suspend fun closeAll() {
        synchronized(active) { active.keys.toList() }.forEach { closeSession(it) }
    }
}
