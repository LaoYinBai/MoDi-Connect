package com.modi.connect.core.infrastructure

import com.modi.connect.core.connectivity.ChannelDirection
import com.modi.connect.core.connectivity.ChannelId
import com.modi.connect.core.connectivity.PeerId
import com.modi.connect.core.connectivity.SessionId
import com.modi.connect.core.connectivity.SessionState
import com.modi.connect.core.connectivity.TransportKind
import java.security.MessageDigest

data class ConnectivityLogContext(
    val peerId: PeerId? = null,
    val sessionId: SessionId? = null,
    val channelId: ChannelId? = null,
    val transport: TransportKind? = null,
    val direction: ChannelDirection? = null,
    val sequence: Long? = null,
    val state: SessionState? = null,
    val operationId: String? = null
) {
    internal fun renderSafe(): String = buildList {
        peerId?.let { add("peer=p:${fingerprint(it.value)}") }
        sessionId?.let { add("session=s:${fingerprint(it.value)}") }
        channelId?.let { add("channel=${it.value}") }
        transport?.let { add("transport=${it.name}") }
        direction?.let { add("direction=${it.name}") }
        sequence?.let { add("sequence=$it") }
        state?.let { add("state=${it.name}") }
        operationId?.takeIf(String::isNotBlank)?.let { add("operation=o:${fingerprint(it)}") }
    }.joinToString(" ")

    private fun fingerprint(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(Charsets.UTF_8))
        .take(6)
        .joinToString("") { "%02x".format(it) }
}

internal object LogSanitizer {
    fun sanitize(message: String): String {
        var safe = BEARER.replace(message, "$1[REDACTED]")
        safe = SECRET_QUERY.replace(safe, "$1[REDACTED]")
        safe = SECRET_VALUE.replace(safe, "$1=[REDACTED]")
        safe = MAC_ADDRESS.replace(safe, "[REDACTED]")
        return TOKEN_LIKE.replace(safe) { match ->
            if (match.value.all { it.isUpperCase() || it == '_' }) match.value else "[REDACTED]"
        }
    }

    private val BEARER = Regex("(?i)(Authorization\\s*:\\s*Bearer\\s+)[^\\s,]+")
    private val SECRET_QUERY = Regex("(?i)([?&](?:access_token|private_token|token)=)[^&#\\s]+")
    private val SECRET_VALUE = Regex("(?i)\\b(token|qr|device_?id|android_?id|serial|imei|mac)\\s*[:=]\\s*[^&,\\s]+")
    private val MAC_ADDRESS = Regex("(?i)\\b(?:[0-9a-f]{2}:){5}[0-9a-f]{2}\\b")
    private val TOKEN_LIKE = Regex("\\b[A-Za-z0-9_-]{24,}\\b")
}
