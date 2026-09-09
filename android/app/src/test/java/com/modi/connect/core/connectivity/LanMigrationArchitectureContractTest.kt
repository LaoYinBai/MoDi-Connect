package com.modi.connect.core.connectivity

import java.nio.file.Files
import java.nio.file.Path
import org.junit.Assert.assertTrue
import org.junit.Test

class LanMigrationArchitectureContractTest {
    @Test
    fun `LAN production slice is build gated and uses target session audio channel`() {
        val gradle = source("build.gradle.kts")
        val lan = source("src/main/java/com/modi/connect/links/wifilan/WifiLanLink.kt")

        assertTrue(gradle.contains("LAN_TARGET_ARCHITECTURE"))
        assertTrue(gradle.contains("modiLanTarget"))
        assertTrue(lan.contains("LanTargetComposition.isEnabled"))
        assertTrue(lan.contains("LanTargetSessionRuntime"))
        assertTrue(lan.contains("startStreamingWithChannel"))
    }

    private fun source(relativePath: String): String = String(Files.readAllBytes(Path.of(relativePath)))
}
