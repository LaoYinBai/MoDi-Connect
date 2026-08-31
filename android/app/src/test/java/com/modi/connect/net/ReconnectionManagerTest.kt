package com.modi.connect.net

import com.modi.connect.ConnectionStateManager
import com.modi.connect.core.enums.NetworkQuality
import com.modi.connect.core.interfaces.INetworkMonitor
import com.modi.connect.core.models.NetworkInfo
import com.modi.protocol.TransportType
import kotlinx.coroutines.*
import kotlinx.coroutines.test.*
import org.junit.Assert.*
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ReconnectionManagerTest {
    private class Network : INetworkMonitor {
        override fun start() = Unit
        override fun stop() = Unit
        override val isConnected = true
        override val activeTransport = TransportType.Udp
        override val quality = NetworkQuality.Good
        override var onNetworkChanged: ((NetworkInfo) -> Unit)? = null
    }

    @Test fun retries_only_original_endpoint_and_stops_after_five_attempts() = runTest {
        val hosts = mutableListOf<String>()
        var stopped = 0
        val manager = ReconnectionManager(ConnectionStateManager(), Network(), { stopped++ },
            { host, _ -> hosts += host; false }, StandardTestDispatcher(testScheduler))
        manager.arm("192.168.1.7", 2)
        manager.triggerRecovery()
        advanceUntilIdle()
        assertEquals(List(5) { "192.168.1.7" }, hosts)
        assertEquals(1, stopped)
        manager.stop()
    }

    @Test fun cancelling_waits_for_inflight_cleanup_and_prevents_restart() = runTest {
        val entered = CompletableDeferred<Unit>()
        var cleaned = false
        var attempts = 0
        val manager = ReconnectionManager(ConnectionStateManager(), Network(), {}, { _, _ ->
            attempts++
            try { entered.complete(Unit); awaitCancellation() }
            finally { cleaned = true }
        }, StandardTestDispatcher(testScheduler))
        manager.arm("192.168.1.7", 2)
        manager.triggerRecovery()
        entered.await()
        manager.cancelRecovery()
        assertTrue(cleaned)
        manager.triggerRecovery()
        advanceUntilIdle()
        assertEquals(1, attempts)
        manager.stop()
    }

    @Test fun a_new_session_uses_latest_route_after_old_recovery_cancelled() = runTest {
        val routes = mutableListOf<Int>()
        val manager = ReconnectionManager(ConnectionStateManager(), Network(), {},
            { _, route -> routes += route; true }, StandardTestDispatcher(testScheduler))
        manager.arm("192.168.1.7", 0)
        manager.updateRoute(2)
        manager.triggerRecovery()
        advanceUntilIdle()
        manager.cancelRecovery()
        manager.arm("192.168.1.8", 3)
        manager.triggerRecovery()
        advanceUntilIdle()
        assertEquals(listOf(2, 3), routes)
        manager.stop()
    }
}
