package com.modi.connect.audio

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class MediaProjectionOwnerTest {
    @Test
    fun replace_registers_before_projection_becomes_current() = runTest {
        val first = FakeProjectionHandle()
        val second = FakeProjectionHandle()
        val owner = MediaProjectionOwner(this) {}

        owner.replace(first)
        assertTrue(owner.hasProjection)
        owner.replace(second)

        assertEquals(1, first.unregisterCalls)
        assertEquals(1, first.stopCalls)
        assertTrue(second.registered)
    }

    @Test
    fun system_stop_clears_projection_and_stops_streaming_once() = runTest {
        val handle = FakeProjectionHandle()
        var stops = 0
        val owner = MediaProjectionOwner(this) { stops++ }
        owner.replace(handle)

        handle.raiseStopped()
        handle.raiseStopped()
        testScheduler.advanceUntilIdle()

        assertFalse(owner.hasProjection)
        assertEquals(1, stops)
        assertEquals(0, handle.stopCalls)
    }

    private class FakeProjectionHandle : ProjectionHandle {
        private var callback: (() -> Unit)? = null
        var registered = false
        var unregisterCalls = 0
        var stopCalls = 0

        override fun register(onStopped: () -> Unit) {
            registered = true
            callback = onStopped
        }

        override fun unregister() { unregisterCalls++ }
        override fun stop() { stopCalls++ }
        override fun projection() = null
        fun raiseStopped() = callback?.invoke()
    }
}
