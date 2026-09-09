package com.modi.connect.core.connectivity

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

class SessionRegistryTest {
    @Test
    fun `shadow events are idempotent and expose no control actions`() {
        val registry = SessionRegistry()
        objectsIn("sessionShadowEvents").forEach { item ->
            val id = SessionId.parse(stringField(item, "session"))
            val transport = TransportKind.valueOf(stringField(item, "transport"))
            val state = SessionState.valueOf(stringField(item, "state"))
            when (stringField(item, "operation")) {
                "start" -> registry.observeStarted(id, transport, state)
                "state" -> registry.observeState(id, state)
                "end" -> registry.observeEnded(id)
                "closeAll" -> registry.observeClosedAll()
            }
        }

        assertEquals(2, registry.snapshots.size)
        registry.snapshots.forEach { assertEquals(SessionState.Closed, it.state) }
        assertFalse(SessionRegistry::class.java.methods.any {
            it.name.contains("connect", true) || it.name.contains("send", true) || it.name.contains("disconnect", true)
        })
    }

    @Test
    fun `unknown or illegal state event does not mutate a session`() {
        val registry = SessionRegistry()
        val missing = registry.observeState(SessionId.parse("missing"), SessionState.Ready)
        registry.observeStarted(SessionId.parse("known"), TransportKind.Lan, SessionState.Ready)
        val illegal = registry.observeState(SessionId.parse("known"), SessionState.Authenticating)

        assertFalse(missing.applied)
        assertFalse(illegal.applied)
        assertEquals(SessionState.Ready, registry.snapshots.single { it.id == SessionId.parse("known") }.state)
    }

    private val vectors = findVectors().readText()
    private fun objectsIn(arrayName: String): List<String> {
        val body = Regex("\\\"$arrayName\\\"\\s*:\\s*\\[(.*?)]", setOf(RegexOption.DOT_MATCHES_ALL))
            .find(vectors)?.groupValues?.get(1) ?: error("Missing array $arrayName")
        return Regex("\\{(.*?)\\}", setOf(RegexOption.DOT_MATCHES_ALL)).findAll(body).map { it.value }.toList()
    }
    private fun stringField(item: String, name: String): String =
        Regex("\\\"$name\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"").find(item)?.groupValues?.get(1)
            ?: error("Missing string $name")
    private fun findVectors(): File {
        var directory = File(System.getProperty("user.dir")).absoluteFile
        repeat(8) {
            File(directory, "scripts/architecture/connectivity-model-vectors.json").takeIf(File::isFile)?.let { return it }
            directory = checkNotNull(directory.parentFile) { "Could not locate vectors" }
        }
        error("Could not locate vectors")
    }
}
