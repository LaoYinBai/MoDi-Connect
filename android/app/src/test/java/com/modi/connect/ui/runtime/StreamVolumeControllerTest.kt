package com.modi.connect.ui.runtime

import com.modi.connect.audio.StreamGain
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.advanceTimeBy
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class StreamVolumeControllerTest {
    @Test
    fun non_streaming_keys_are_not_consumed() = runTest {
        val controller = StreamVolumeController(backgroundScope)
        var volume = 0.5f

        val consumed = controller.adjust(false, volume, StreamGain.HARDWARE_KEY_STEP, { volume = it }, {})

        assertFalse(consumed)
        assertEquals(0.5f, volume, 0.0001f)
    }

    @Test
    fun latest_key_press_restarts_the_hud_timeout() = runTest {
        val controller = StreamVolumeController(backgroundScope)
        var volume = 14f / 15f
        var hud = false

        assertTrue(controller.adjust(true, volume, StreamGain.HARDWARE_KEY_STEP, { volume = it }, { hud = it }))
        assertEquals(1f, volume, 0.0001f)
        assertTrue(hud)

        advanceTimeBy(1_000)
        controller.adjust(true, volume, -StreamGain.HARDWARE_KEY_STEP, { volume = it }, { hud = it })
        advanceTimeBy(1_000)
        runCurrent()
        assertTrue(hud)

        advanceTimeBy(201)
        runCurrent()
        assertFalse(hud)
        assertEquals(14f / 15f, volume, 0.0001f)
    }
}
