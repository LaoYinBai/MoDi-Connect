package com.modi.connect.core.infrastructure

import com.modi.connect.core.connectivity.ChannelDirection
import com.modi.connect.core.connectivity.ChannelId
import com.modi.connect.core.connectivity.PeerId
import com.modi.connect.core.connectivity.SessionId
import com.modi.connect.core.connectivity.SessionState
import com.modi.connect.core.connectivity.TransportKind
import com.modi.connect.core.interfaces.ILogger
import com.modi.connect.core.impl.ExportableLogger
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ConnectivityLoggingTest {
    @After
    fun restoreLogger() {
        Log.setImpl(ExportableLogger)
    }

    @Test
    fun `context is correlatable without exposing complete identifiers`() {
        val logger = RecordingLogger()
        Log.setImpl(logger)
        val context = ConnectivityLogContext(
            peerId = PeerId.parse("private-device-123456"),
            sessionId = SessionId.parse("session-secret-987654"),
            channelId = ChannelId.parse("audio/primary"),
            transport = TransportKind.WifiDirect,
            direction = ChannelDirection.Receive,
            sequence = 42,
            state = SessionState.Streaming
        )

        Log.i("connection", "token=not-for-logs", context)
        Log.i("connection", "second event", context)

        assertEquals(2, logger.messages.size)
        assertEquals(logger.messages[0].substringBefore(" channel="), logger.messages[1].substringBefore(" channel="))
        assertTrue(logger.messages[0].contains("channel=audio/primary"))
        assertTrue(logger.messages[0].contains("transport=WifiDirect"))
        assertTrue(logger.messages[0].contains("sequence=42"))
        assertFalse(logger.messages.joinToString().contains("private-device-123456"))
        assertFalse(logger.messages.joinToString().contains("session-secret-987654"))
        assertFalse(logger.messages.joinToString().contains("not-for-logs"))
    }

    @Test
    fun `legacy logging api remains available`() {
        val logger = RecordingLogger()
        Log.setImpl(logger)
        Log.i("legacy", "still supported")
        assertEquals(listOf("still supported"), logger.messages)
    }

    @Test
    fun `concurrent session contexts keep distinct safe correlation ids`() {
        val logger = RecordingLogger()
        Log.setImpl(logger)

        Log.i("connection", "session A", ConnectivityLogContext(
            sessionId = SessionId.parse("phone-a"), channelId = ChannelId.parse("audio/primary"), sequence = 0
        ))
        Log.i("connection", "session B", ConnectivityLogContext(
            sessionId = SessionId.parse("phone-b"), channelId = ChannelId.parse("audio/primary"), sequence = 0
        ))

        assertEquals(2, logger.messages.size)
        assertTrue(logger.messages.all { it.contains("channel=audio/primary") && it.contains("sequence=0") })
        assertFalse(logger.messages[0].substringBefore(" channel=") == logger.messages[1].substringBefore(" channel="))
    }

    private class RecordingLogger : ILogger {
        val messages = mutableListOf<String>()
        override fun debug(tag: String, msg: String) { messages += msg }
        override fun info(tag: String, msg: String) { messages += msg }
        override fun warn(tag: String, msg: String) { messages += msg }
        override fun error(tag: String, msg: String) { messages += msg }
        override fun error(tag: String, msg: String, ex: Exception) { messages += msg }
    }
}
