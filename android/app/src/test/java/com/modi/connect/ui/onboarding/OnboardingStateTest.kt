package com.modi.connect.ui.onboarding

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class OnboardingStateTest {
    @Test
    fun four_steps_are_bounded_and_back_is_safe() {
        var state = OnboardingState()
        repeat(10) { state = state.next() }
        assertEquals(3, state.stepIndex)
        repeat(10) { state = state.back() }
        assertEquals(0, state.stepIndex)
    }

    @Test
    fun complete_and_skip_are_permanent_until_settings_reset() {
        val memory = InMemoryOnboardingPersistence()
        val store = OnboardingStore(memory)
        assertTrue(store.shouldShow())
        store.skip()
        assertFalse(store.shouldShow())
        store.reset()
        assertTrue(store.shouldShow())
        store.complete()
        assertFalse(store.shouldShow())
    }

    @Test
    fun permission_denial_returns_to_a_readable_explanation() {
        val state = OnboardingState(stepIndex = 2).permissionDenied("麦克风")
        assertEquals(2, state.stepIndex)
        assertTrue(state.explanation!!.contains("麦克风"))
    }
}
