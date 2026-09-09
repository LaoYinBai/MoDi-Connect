package com.modi.connect.core.connectivity

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class BuiltInModuleRegistryTest {
    @Test
    fun `registered capabilities match modules and one failure does not stop another`() = runTest {
        val sessionId = SessionId.parse("phone-a")
        val sessionCore = readySession(sessionId)
        var audioHandled = 0
        var audioStopped = 0
        var clipboardStopped = 0
        val audio = BuiltInModules.audioModule(
            handle = { _, _ -> audioHandled++ },
            stop = { audioStopped++ },
        )
        val clipboard = BuiltInModules.clipboardModule(
            handle = { _, _ -> error("module failed") },
            stop = { clipboardStopped++ },
        )
        val registry = BuiltInModuleRegistry(listOf(audio, clipboard))
        val audioChannel = MemoryChannelDataPlane(sessionId, AudioChannel.descriptor)
        val clipboardChannel = MemoryChannelDataPlane(sessionId, ClipboardChannel.descriptor)

        assertEquals(
            listOf(ConnectivityCapability.Audio, ConnectivityCapability.Clipboard),
            registry.registeredCapabilities
        )
        assertTrue(registry.activate(sessionId, "audio", audioChannel).succeeded)
        assertTrue(registry.activate(sessionId, "clipboard", clipboardChannel).succeeded)

        assertTrue(registry.dispatch(sessionId, "audio", byteArrayOf(1, 2)).succeeded)
        val failed = registry.dispatch(sessionId, "clipboard", byteArrayOf(3))
        assertFalse(failed.succeeded)
        assertEquals(BuiltInModuleState.Failed, failed.state)
        assertTrue(registry.dispatch(sessionId, "audio", byteArrayOf(4)).succeeded)
        assertEquals(2, audioHandled)
        assertEquals(SessionState.Ready, sessionCore.snapshots.single().state)
        assertEquals(BuiltInModuleState.Ready, registry.snapshots.single { it.moduleId == "audio" }.state)

        registry.stopSession(sessionId)

        assertEquals(1, audioStopped)
        assertEquals(1, clipboardStopped)
        assertEquals(1, audioChannel.closeCount)
        assertEquals(1, clipboardChannel.closeCount)
        registry.snapshots.forEach { assertEquals(BuiltInModuleState.Closed, it.state) }
    }

    @Test
    fun `missing module is reported without changing ready session`() = runTest {
        val sessionId = SessionId.parse("phone-a")
        val sessionCore = readySession(sessionId)
        val registry = BuiltInModuleRegistry(listOf(BuiltInModules.audioModule(handle = { _, _ -> })))

        val result = registry.activate(
            sessionId,
            "clipboard",
            MemoryChannelDataPlane(sessionId, ClipboardChannel.descriptor)
        )

        assertFalse(result.succeeded)
        assertEquals(BuiltInModuleState.Unavailable, result.state)
        assertEquals(SessionState.Ready, sessionCore.snapshots.single().state)
        assertEquals(listOf(ConnectivityCapability.Audio), registry.registeredCapabilities)
    }

    private fun readySession(id: SessionId) = SessionRegistry().also {
        assertTrue(it.observeStarted(id, TransportKind.Lan, SessionState.Ready).applied)
    }

    private class MemoryChannelDataPlane(
        override val sessionId: SessionId,
        override val descriptor: ChannelDescriptor
    ) : ChannelDataPlane {
        override var state = ChannelRuntimeState.Closed
        override val nextSequence = 0L
        var closeCount = 0
        override var onBytesReceived: ((ByteArray) -> Unit)? = null
        override suspend fun open() { state = ChannelRuntimeState.Open }
        override fun reserveSequence() = 0L
        override fun resetSequence() = Unit
        override suspend fun send(payload: ByteArray) = Unit
        override suspend fun close() { closeCount++; state = ChannelRuntimeState.Closed }
    }
}
