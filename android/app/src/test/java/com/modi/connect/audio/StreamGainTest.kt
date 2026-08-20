package com.modi.connect.audio

import com.modi.connect.ui.runtime.StreamGainStore
import org.junit.Assert.assertEquals
import org.junit.Test

class StreamGainTest {
    @Test
    fun values_are_clamped_and_adjusted_in_hardware_key_steps() {
        val gain = StreamGain(0.5f)

        gain.set(2f)
        assertEquals(1f, gain.value, 0.0001f)
        gain.adjust(-StreamGain.HARDWARE_KEY_STEP)
        assertEquals(14f / 15f, gain.value, 0.0001f)
        gain.set(-1f)
        assertEquals(0f, gain.value, 0.0001f)
    }

    @Test
    fun current_value_can_be_reapplied_after_capturer_recreation() {
        val gain = StreamGain(0.4f)
        val applied = mutableListOf<Float>()

        gain.applyTo { applied += it }
        gain.applyTo { applied += it }

        assertEquals(listOf(0.4f, 0.4f), applied)
    }

    @Test
    fun persisted_values_are_normalized_at_the_store_boundary() {
        assertEquals(0f, StreamGainStore.normalize(-0.5f), 0.0001f)
        assertEquals(0.4f, StreamGainStore.normalize(0.4f), 0.0001f)
        assertEquals(1f, StreamGainStore.normalize(2f), 0.0001f)
        assertEquals(1f, StreamGainStore.normalize(Float.NaN), 0.0001f)
    }
}
