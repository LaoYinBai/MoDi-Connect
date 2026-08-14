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
        AndroidLog.d(tag, msg)
        record("D", tag, msg)
    }

    override fun info(tag: String, msg: String) {
        AndroidLog.i(tag, msg)
        record("I", tag, msg)
    }

    override fun warn(tag: String, msg: String) {
        AndroidLog.w(tag, msg)
        record("W", tag, msg)
    }

    override fun error(tag: String, msg: String) {
        AndroidLog.e(tag, msg)
        record("E", tag, msg)
    }

    override fun error(tag: String, msg: String, ex: Exception) {
        AndroidLog.e(tag, msg, ex)
        record("E", tag, "$msg: ${ex.javaClass.simpleName}: ${ex.message.orEmpty()}")
    }

    fun snapshot(): String = synchronized(lines) {
        if (lines.isEmpty()) "本次会话暂无应用日志" else lines.joinToString("\n")
    }

    private fun record(level: String, tag: String, message: String) {
        val line = "${LocalTime.now().format(timeFormat)} $level/$tag: $message"
        synchronized(lines) {
            if (lines.size == MAX_LINES) lines.removeFirst()
            lines.addLast(line)
        }
    }
}
