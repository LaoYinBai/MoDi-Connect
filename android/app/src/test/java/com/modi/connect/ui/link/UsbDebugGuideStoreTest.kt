package com.modi.connect.ui.link

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class UsbDebugGuideStoreTest {
    @Test
    fun guide_is_shown_only_for_the_first_usb_selection() {
        val persistence = InMemoryUsbDebugGuidePersistence()
        val store = UsbDebugGuideStore(persistence)

        assertTrue(store.shouldShowForUsbSelection())
        store.markShown()
        assertFalse(store.shouldShowForUsbSelection())
    }
}
