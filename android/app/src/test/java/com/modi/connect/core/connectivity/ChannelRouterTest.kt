package com.modi.connect.core.connectivity

import java.io.File
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test

class ChannelRouterTest {
    @Test
    fun `same audio channel id is isolated by session and owns sequence`() = runTest {
        val router = ChannelRouter()
        objectsIn("audioChannelSessions").forEach { item ->
            val plane = MemoryChannelDataPlane(SessionId.parse(stringField(item, "session")))
            router.register(plane)
            plane.open()
            numberArray(item, "sequences").forEach { expected -> assertEquals(expected, plane.reserveSequence()) }
            plane.resetSequence()
            assertEquals(numberField(item, "afterReset"), plane.reserveSequence())
        }
        assertEquals(2, router.channels.size)
        router.channels.forEach { assertEquals(AudioChannel.primary, it.descriptor.id) }
    }

    private class MemoryChannelDataPlane(override val sessionId: SessionId) : ChannelDataPlane {
        private var next = 0L
        override val descriptor = AudioChannel.descriptor
        override var state = ChannelRuntimeState.Closed
        override val nextSequence get() = next
        override var onBytesReceived: ((ByteArray) -> Unit)? = null
        override suspend fun open() { state = ChannelRuntimeState.Open; resetSequence() }
        override fun reserveSequence(): Long = next++
        override fun resetSequence() { next = 0 }
        override suspend fun send(payload: ByteArray) = Unit
        override suspend fun close() { state = ChannelRuntimeState.Closed }
    }

    private val vectors = findVectors().readText()
    private fun objectsIn(arrayName: String): List<String> {
        val body = Regex("\\\"$arrayName\\\"\\s*:\\s*\\[(.*)]\\s*}", setOf(RegexOption.DOT_MATCHES_ALL))
            .find(vectors)?.groupValues?.get(1) ?: error("Missing array $arrayName")
        return Regex("\\{(.*?)\\}", setOf(RegexOption.DOT_MATCHES_ALL)).findAll(body).map { it.value }.toList()
    }
    private fun stringField(item: String, name: String): String =
        Regex("\\\"$name\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"").find(item)?.groupValues?.get(1) ?: error("Missing $name")
    private fun numberField(item: String, name: String): Long =
        Regex("\\\"$name\\\"\\s*:\\s*(\\d+)").find(item)?.groupValues?.get(1)?.toLong() ?: error("Missing $name")
    private fun numberArray(item: String, name: String): List<Long> =
        Regex("\\\"$name\\\"\\s*:\\s*\\[([^]]*)]").find(item)?.groupValues?.get(1)
            ?.split(',')?.filter { it.isNotBlank() }?.map { it.trim().toLong() } ?: error("Missing $name")
    private fun findVectors(): File {
        var directory = File(System.getProperty("user.dir")).absoluteFile
        repeat(8) {
            File(directory, "scripts/architecture/connectivity-model-vectors.json").takeIf(File::isFile)?.let { return it }
            directory = checkNotNull(directory.parentFile) { "Could not locate vectors" }
        }
        error("Could not locate vectors")
    }
}
