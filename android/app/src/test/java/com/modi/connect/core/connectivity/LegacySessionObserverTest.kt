package com.modi.connect.core.connectivity

import com.modi.connect.ConnectionState
import com.modi.connect.core.connectivity.session.LegacySessionObserver
import com.modi.connect.core.connectivity.session.SessionShadowComposition
import java.util.UUID
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Test

class LegacySessionObserverTest {
    @Test
    fun `observer projects only the current legacy session`() {
        val registry = SessionRegistry()
        val observer = LegacySessionObserver(registry)
        val id = UUID.fromString("11111111-1111-1111-1111-111111111111")

        observer.observeState(ConnectionState.STREAMING)
        observer.observeStarted(id, TransportKind.Lan, ConnectionState.CONNECTING)
        observer.observeState(ConnectionState.CONNECTED)
        observer.observeState(ConnectionState.STREAMING)
        observer.observeEnded(id)

        assertEquals(SessionState.Closed, registry.snapshots.single().state)
        assertFalse(LegacySessionObserver::class.java.methods.any {
            it.name.contains("connect", true) || it.name.contains("send", true) || it.name.contains("disconnect", true)
        })
    }

    @Test
    fun `production shadow gate is off by default`() {
        assertNull(SessionShadowComposition.createIfEnabled { null })
    }
}
