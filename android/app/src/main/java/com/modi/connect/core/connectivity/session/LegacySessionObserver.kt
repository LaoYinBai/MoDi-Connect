package com.modi.connect.core.connectivity.session

import com.modi.connect.ConnectionState
import com.modi.connect.core.connectivity.SessionId
import com.modi.connect.core.connectivity.SessionObservationResult
import com.modi.connect.core.connectivity.SessionRegistry
import com.modi.connect.core.connectivity.SessionState
import com.modi.connect.core.connectivity.TransportKind
import java.util.UUID

class LegacySessionObserver(
    val registry: SessionRegistry,
    private val differenceReporter: (String) -> Unit = {},
) {
    private var current: SessionId? = null

    fun observeStarted(id: UUID, transport: TransportKind, state: ConnectionState): SessionObservationResult {
        val sessionId = SessionId.parse(id.toString().replace("-", ""))
        current = sessionId
        return report(registry.observeStarted(sessionId, transport, state.toSessionState()))
    }

    fun observeState(state: ConnectionState): SessionObservationResult {
        val id = current ?: return report(SessionObservationResult(false, "No active legacy session."))
        if (state == ConnectionState.CONNECTED &&
            registry.snapshots.firstOrNull { it.id == id }?.state == SessionState.Connecting
        ) {
            report(registry.observeState(id, SessionState.Authenticating))
        }
        return report(registry.observeState(id, state.toSessionState()))
    }

    fun observeEnded(id: UUID): SessionObservationResult {
        val sessionId = SessionId.parse(id.toString().replace("-", ""))
        val result = registry.observeEnded(sessionId)
        if (current == sessionId) current = null
        return report(result)
    }

    fun observeClosedAll() {
        registry.observeClosedAll()
        current = null
    }

    private fun ConnectionState.toSessionState(): SessionState = when (this) {
        ConnectionState.IDLE, ConnectionState.DISCONNECTED -> SessionState.Closed
        ConnectionState.SEARCHING, ConnectionState.FOUND -> SessionState.Discovering
        ConnectionState.CONNECTING -> SessionState.Connecting
        ConnectionState.CONNECTED -> SessionState.Ready
        ConnectionState.STREAMING -> SessionState.Streaming
        ConnectionState.RECONNECTING -> SessionState.Reconnecting
        ConnectionState.ERROR -> SessionState.Failed
    }

    private fun report(result: SessionObservationResult): SessionObservationResult {
        if (!result.applied) differenceReporter("Legacy observation not applied: ${result.reason}")
        return result
    }
}

object SessionShadowComposition {
    fun createIfEnabled(readSetting: (String) -> String? = System::getProperty): LegacySessionObserver? =
        if (readSetting("modi.session.shadow") == "1") {
            LegacySessionObserver(SessionRegistry()) { message ->
                com.modi.connect.core.infrastructure.Log.w("SessionShadow", message)
            }
        } else null
}
