package com.modi.connect.ui.theme

import org.junit.Assert.*
import org.junit.Test

class ThemeModeTest {
    @Test fun manualSelectionOverridesSystem() {
        assertTrue(ThemeMode.INK.isDark(false))
        assertTrue(ThemeMode.INK.isDark(true))
        assertFalse(ThemeMode.PAPER.isDark(true))
        assertFalse(ThemeMode.PAPER.isDark(false))
    }
    @Test fun systemSelectionTracksBothSystemStates() {
        assertTrue(ThemeMode.SYSTEM.isDark(true))
        assertFalse(ThemeMode.SYSTEM.isDark(false))
    }
    @Test fun missingOrInvalidStoredChoiceFallsBackToSystem() {
        assertEquals(ThemeMode.SYSTEM, ThemeMode.fromStored(null))
        assertEquals(ThemeMode.SYSTEM, ThemeMode.fromStored("unexpected"))
        assertEquals(ThemeMode.INK, ThemeMode.fromStored("ink"))
        assertEquals(ThemeMode.PAPER, ThemeMode.fromStored("paper"))
    }
}
