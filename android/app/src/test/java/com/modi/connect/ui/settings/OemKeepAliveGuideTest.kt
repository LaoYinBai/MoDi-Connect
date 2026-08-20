package com.modi.connect.ui.settings

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class OemKeepAliveGuideTest {
    @Test
    fun known_and_unknown_manufacturers_always_end_with_application_details_fallback() {
        val manufacturers = listOf("Xiaomi", "Redmi", "Huawei", "Honor", "OPPO", "realme", "vivo", "iQOO", "Samsung", "unknown")

        manufacturers.forEach { manufacturer ->
            val intents = OemKeepAliveGuide.resolve(manufacturer)
            assertTrue("$manufacturer must have at least one candidate", intents.isNotEmpty())
            assertEquals(KeepAliveIntentSpec.ApplicationDetails, intents.last())
        }
    }

    @Test
    fun xiaomi_and_huawei_have_vendor_specific_candidates_before_fallback() {
        assertTrue(OemKeepAliveGuide.resolve("XIAOMI").first() is KeepAliveIntentSpec.Component)
        assertTrue(OemKeepAliveGuide.resolve("huawei").first() is KeepAliveIntentSpec.Component)
    }
}
