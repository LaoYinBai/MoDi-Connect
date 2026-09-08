package com.modi.connect.core.impl

import android.util.Log as AndroidLog
import com.modi.connect.core.interfaces.ILogger
import com.modi.connect.core.infrastructure.LogSanitizer
import java.time.LocalTime
import java.time.format.DateTimeFormatter
import java.util.ArrayDeque

object ExportableLogger : ILogger {
    private const val MAX_LINES = 400
    private val timeFormat = DateTimeFormatter.ofPattern("HH:mm:ss.SSS")
    private val lines = ArrayDeque<String>(MAX_LINES)

    override fun debug(tag: String, msg: String) {
        val safe = sanitizeForExport(msg)
        AndroidLog.d(tag, safe)
        record("D", tag, safe)
    }

    override fun info(tag: String, msg: String) {
        val safe = sanitizeForExport(msg)
        AndroidLog.i(tag, safe)
        record("I", tag, safe)
    }

    override fun warn(tag: String, msg: String) {
        val safe = sanitizeForExport(msg)
        AndroidLog.w(tag, safe)
        record("W", tag, safe)
    }

    override fun error(tag: String, msg: String) {
        val safe = sanitizeForExport(msg)
        AndroidLog.e(tag, safe)
        record("E", tag, safe)
    }

    override fun error(tag: String, msg: String, ex: Exception) {
        val safe = sanitizeForExport("$msg: ${ex.javaClass.simpleName}: ${ex.message.orEmpty()}")
        AndroidLog.e(tag, safe)
        record("E", tag, safe)
    }

    fun snapshot(): String = synchronized(lines) {
        if (lines.isEmpty()) "本次会话暂无应用日志" else lines.joinToString("\n")
    }

    internal fun sanitizeForExport(message: String): String = LogSanitizer.sanitize(message)

    private fun record(level: String, tag: String, message: String) {
        val line = "${LocalTime.now().format(timeFormat)} $level/$tag: $message"
        synchronized(lines) {
            if (lines.size == MAX_LINES) lines.removeFirst()
            lines.addLast(line)
        }
    }

}
