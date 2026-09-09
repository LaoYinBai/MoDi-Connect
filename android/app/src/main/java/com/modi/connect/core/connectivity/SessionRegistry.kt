package com.modi.connect.core.connectivity

data class SessionSnapshot(
    val id: SessionId,
    val transport: TransportKind,
    val state: SessionState,
    val revision: Long,
)

data class SessionObservationResult(val applied: Boolean, val reason: String? = null)

/** Observation-only registry. It intentionally owns no connection or data-plane controls. */
class SessionRegistry {
    private val lock = Any()
    private val sessions = linkedMapOf<SessionId, SessionSnapshot>()
    private var revision = 0L

    val snapshots: List<SessionSnapshot>
        get() = synchronized(lock) { sessions.values.sortedBy { it.id.value }.toList() }

    fun observeStarted(id: SessionId, transport: TransportKind, state: SessionState): SessionObservationResult =
        synchronized(lock) {
            val existing = sessions[id]
            if (existing == null) {
                sessions[id] = SessionSnapshot(id, transport, state, ++revision)
                SessionObservationResult(true)
            } else if (existing.transport != transport) {
                SessionObservationResult(false, "Session transport changed.")
            } else {
                observeStateLocked(existing, state)
            }
        }

    fun observeState(id: SessionId, state: SessionState): SessionObservationResult = synchronized(lock) {
        val existing = sessions[id] ?: return@synchronized SessionObservationResult(false, "Unknown session.")
        observeStateLocked(existing, state)
    }

    fun observeEnded(id: SessionId): SessionObservationResult = synchronized(lock) {
        val existing = sessions[id] ?: return@synchronized SessionObservationResult(false, "Unknown session.")
        if (existing.state != SessionState.Closed) {
            sessions[id] = existing.copy(state = SessionState.Closed, revision = ++revision)
        }
        SessionObservationResult(true)
    }

    fun observeClosedAll() = synchronized(lock) {
        sessions.toMap().forEach { (id, snapshot) ->
            if (snapshot.state != SessionState.Closed) {
                sessions[id] = snapshot.copy(state = SessionState.Closed, revision = ++revision)
            }
        }
    }

    private fun observeStateLocked(existing: SessionSnapshot, state: SessionState): SessionObservationResult {
        if (existing.state == state) return SessionObservationResult(true)
        if (!SessionStateMachine.canTransition(existing.state, state)) {
            return SessionObservationResult(false, "Illegal transition ${existing.state} -> $state.")
        }
        sessions[existing.id] = existing.copy(state = state, revision = ++revision)
        return SessionObservationResult(true)
    }
}
