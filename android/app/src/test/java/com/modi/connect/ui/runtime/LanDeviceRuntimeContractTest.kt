package com.modi.connect.ui.runtime

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.nio.file.Files
import java.nio.file.Path

class LanDeviceRuntimeContractTest {
    @Test
    fun `discovery coordinator owns adaptation and runtime keeps same LAN peer switching`() {
        val runtime = source("src/main/java/com/modi/connect/ui/runtime/MoDiRuntime.kt")
        val discovery = source("src/main/java/com/modi/connect/ui/runtime/DiscoveryCoordinator.kt")

        assertTrue(runtime.contains("suspend fun selectLanDevice(device: LanDeviceUiModel)"))
        assertTrue(runtime.contains("forceCurrent = true"))
        assertTrue(discovery.contains("LanDevicePanelState.from"))
        assertTrue(discovery.contains("fun found("))
        assertFalse(runtime.contains("mutableStateListOf<LanDeviceUiModel>"))
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
