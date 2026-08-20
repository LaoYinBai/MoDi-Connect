package com.modi.connect.audio

import com.modi.connect.core.impl.ExportableLogger
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AudioFailureClassificationTest {
    @Test
    fun export_redacts_credentials_query_values_and_device_identifiers_but_keeps_event_codes() {
        val raw = "BT_CLOSE_FAILED https://gitee.com/api/v5/repos/x?access_token=secret123&ref=main " +
            "Authorization: Bearer bearer-secret deviceId=phone-42 mac=AA:BB:CC:DD:EE:FF"

        val safe = ExportableLogger.sanitizeForExport(raw)

        assertTrue(safe.contains("BT_CLOSE_FAILED"))
        assertTrue(safe.contains("ref=main"))
        assertFalse(safe.contains("secret123"))
        assertFalse(safe.contains("bearer-secret"))
        assertFalse(safe.contains("phone-42"))
        assertFalse(safe.contains("AA:BB:CC:DD:EE:FF"))
    }
}
