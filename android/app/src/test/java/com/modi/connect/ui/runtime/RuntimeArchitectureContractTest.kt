package com.modi.connect.ui.runtime

import java.nio.file.Files
import java.nio.file.Path
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RuntimeArchitectureContractTest {
    @Test
    fun `runtime delegates platform services and settings to lifecycle owners`() {
        val runtime = source("src/main/java/com/modi/connect/ui/runtime/MoDiRuntime.kt")
        listOf(
            "ConnectivityManager",
            "NetworkCapabilities",
            "P2pPairStore",
            "BatteryOptimizationController",
            "StreamingIntentStore",
            "StreamGainStore",
        ).forEach { forbidden -> assertFalse("runtime still owns $forbidden", runtime.contains(forbidden)) }

        listOf(
            "DiscoveryCoordinator.kt",
            "ConnectionCoordinator.kt",
            "AudioUseCases.kt",
            "ForegroundExecutionController.kt",
            "DiagnosticsFacade.kt",
            "RuntimeSettingsRepository.kt",
        ).forEach { name ->
            assertTrue(Files.isRegularFile(Path.of("src/main/java/com/modi/connect/ui/runtime/$name")))
        }
    }

    private fun source(relativePath: String): String = String(Files.readAllBytes(Path.of(relativePath)))
}
