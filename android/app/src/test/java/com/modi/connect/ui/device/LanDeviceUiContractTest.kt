package com.modi.connect.ui.device

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.nio.file.Files
import java.nio.file.Path

class LanDeviceUiContractTest {
    @Test
    fun `device entry is accessible and panel exposes the approved hierarchy`() {
        val button = source("src/main/java/com/modi/connect/ui/device/LanDeviceButton.kt")
        val panel = source("src/main/java/com/modi/connect/ui/device/LanDevicePanel.kt")

        assertTrue(button.contains("size(48.dp)"))
        assertTrue(button.contains("局域网电脑设备"))
        assertTrue(panel.contains("当前连接"))
        assertTrue(panel.contains("扫描到的设备"))
        assertTrue(panel.contains("MaterialTheme.typography.titleSmall"))
        assertTrue(panel.contains("MaterialTheme.typography.bodySmall"))
        assertTrue(panel.contains("正在寻找同一局域网内的电脑"))
    }

    @Test
    fun `device components depend only on UI models`() {
        val source = source("src/main/java/com/modi/connect/ui/device/LanDevicePanel.kt") +
            source("src/main/java/com/modi/connect/ui/device/LanDeviceButton.kt")

        assertFalse(source.contains("MoDiDiscovery"))
        assertFalse(source.contains("LinkManager"))
        assertFalse(source.contains("NsdManager"))
        assertFalse(source.contains("WifiLanLink"))
    }

    private fun source(relativePath: String): String = String(
        Files.readAllBytes(Path.of(relativePath)),
    )
}
