package com.modi.connect.ui.runtime

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.nio.file.Files
import java.nio.file.Path

class LanDeviceRuntimeContractTest {
    @Test
    fun `runtime owns discovery adaptation and same LAN peer switching`() {
        val source = source("src/main/java/com/modi/connect/ui/runtime/MoDiRuntime.kt")

        assertTrue(source.contains("suspend fun selectLanDevice(device: LanDeviceUiModel)"))
        assertTrue(source.contains("forceCurrent = true"))
        assertTrue(source.contains("LanDevicePanelState.from"))
        assertTrue(source.contains("connectedDevice"))
    }

    @Test
    fun `audio UI remains behind the LAN device model boundary`() {
        val source = source("src/main/java/com/modi/connect/ui/audio/AudioScreen.kt")

        assertFalse(source.contains("MoDiDiscovery"))
        assertFalse(source.contains("LinkManager"))
        assertFalse(source.contains("NsdManager"))
    }

    private fun source(relativePath: String): String = String(
        Files.readAllBytes(Path.of(relativePath)),
    )
}
