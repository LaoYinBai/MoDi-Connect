package com.modi.connect.core.impl

import android.util.Log as AndroidLog
import com.modi.connect.core.interfaces.ILogger
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

    internal fun sanitizeForExport(message: String): String {
        var safe = BEARER.replace(message, "$1[REDACTED]")
        safe = SECRET_QUERY.replace(safe, "$1[REDACTED]")
        safe = DEVICE_VALUE.replace(safe, "$1=[REDACTED]")
        safe = MAC_ADDRESS.replace(safe, "[REDACTED]")
        return TOKEN_LIKE.replace(safe) { match ->
            if (match.value.all { it.isUpperCase() || it == '_' }) match.value else "[REDACTED]"
        }
    }

    private fun record(level: String, tag: String, message: String) {
        val line = "${LocalTime.now().format(timeFormat)} $level/$tag: $message"
        synchronized(lines) {
            if (lines.size == MAX_LINES) lines.removeFirst()
            lines.addLast(line)
        }
    }

    private val BEARER = Regex("(?i)(Authorization\\s*:\\s*Bearer\\s+)[^\\s,]+")
    private val SECRET_QUERY = Regex("(?i)([?&](?:access_token|private_token|token)=)[^&#\\s]+")
    private val DEVICE_VALUE = Regex("(?i)\\b(device_?id|android_?id|serial|imei|mac)\\s*[:=]\\s*[^&,\\s]+")
    private val MAC_ADDRESS = Regex("(?i)\\b(?:[0-9a-f]{2}:){5}[0-9a-f]{2}\\b")
    private val TOKEN_LIKE = Regex("\\b[A-Za-z0-9_-]{24,}\\b")
}
