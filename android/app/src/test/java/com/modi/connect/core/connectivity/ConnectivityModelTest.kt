package com.modi.connect.core.connectivity

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ConnectivityModelTest {
    private val vectors = findVectors().readText()

    @Test
    fun `peer identity is opaque case sensitive and not a display name`() {
        objectsIn("peerIdentities").forEach { item ->
            val left = PeerId.parse(stringField(item, "left"))
            val right = PeerId.parse(stringField(item, "right"))
            assertEquals(booleanField(item, "equal"), left == right)
            assertEquals(stringField(item, "left"), left.toString())
        }
    }

    @Test
    fun `channel id validation matches shared vectors`() {
        objectsIn("channelIds").forEach { item ->
            assertEquals(booleanField(item, "valid"), ChannelId.tryParse(stringField(item, "value")) != null)
        }
    }

    @Test
    fun `session transitions match shared vectors`() {
        objectsIn("stateTransitions").forEach { item ->
            val from = SessionState.valueOf(stringField(item, "from"))
            val to = SessionState.valueOf(stringField(item, "to"))
            assertEquals(booleanField(item, "allowed"), SessionStateMachine.canTransition(from, to))
        }
    }

    @Test
    fun `current vocabulary is platform neutral`() {
        assertEquals(listOf("Lan", "WifiDirect", "Bluetooth", "Usb"), TransportKind.entries.map { it.name })
        assertEquals(listOf("Audio", "Clipboard"), ChannelKind.entries.map { it.name })
        assertEquals(listOf("Send", "Receive", "Duplex"), ChannelDirection.entries.map { it.name })
        assertEquals(
            listOf("None", "Unavailable", "Unauthorized", "Timeout", "TransportFailure", "ProtocolFailure", "Cancelled"),
            ConnectivityErrorCode.entries.map { it.name }
        )
    }

    @Test
    fun `blank peer and session ids are rejected`() {
        assertFalse(PeerId.tryParse(" ") != null)
        assertFalse(SessionId.tryParse("") != null)
        assertTrue(SessionId.tryParse("session-01") != null)
    }

    private fun objectsIn(arrayName: String): List<String> {
        val body = Regex("\\\"$arrayName\\\"\\s*:\\s*\\[(.*?)]", setOf(RegexOption.DOT_MATCHES_ALL))
            .find(vectors)?.groupValues?.get(1) ?: error("Missing array $arrayName")
        return Regex("\\{(.*?)\\}", setOf(RegexOption.DOT_MATCHES_ALL)).findAll(body).map { it.value }.toList()
    }

    private fun stringField(item: String, name: String): String =
        Regex("\\\"$name\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"").find(item)?.groupValues?.get(1)
            ?: error("Missing string $name")

    private fun booleanField(item: String, name: String): Boolean =
        Regex("\\\"$name\\\"\\s*:\\s*(true|false)").find(item)?.groupValues?.get(1)?.toBooleanStrict()
            ?: error("Missing boolean $name")

    private fun findVectors(): File {
        val workingDirectory = System.getProperty("user.dir") ?: error("user.dir is unavailable")
        var directory = File(workingDirectory).absoluteFile
        repeat(8) { depth ->
            File(directory, "scripts/architecture/connectivity-model-vectors.json").takeIf(File::isFile)?.let { return it }
            directory = directory.parentFile
                ?: error("Could not locate connectivity-model-vectors.json after $depth parent directories")
        }
        error("Could not locate connectivity-model-vectors.json from $workingDirectory")
    }
}
