package com.modi.connect.core.connectivity

private const val MAX_IDENTIFIER_LENGTH = 128

@JvmInline
value class PeerId private constructor(val value: String) {
    override fun toString(): String = value

    companion object {
        fun parse(value: String): PeerId = tryParse(value)
            ?: throw IllegalArgumentException("PeerId must be a non-empty opaque identifier")

        fun tryParse(value: String?): PeerId? = value
            ?.takeIf { it.isNotBlank() && it.length <= MAX_IDENTIFIER_LENGTH && it == it.trim() }
            ?.let(::PeerId)
    }
}

@JvmInline
value class SessionId private constructor(val value: String) {
    override fun toString(): String = value

    companion object {
        fun parse(value: String): SessionId = tryParse(value)
            ?: throw IllegalArgumentException("SessionId must be a non-empty opaque identifier")

        fun tryParse(value: String?): SessionId? = value
            ?.takeIf { it.isNotBlank() && it.length <= MAX_IDENTIFIER_LENGTH && it == it.trim() }
            ?.let(::SessionId)
    }
}

@JvmInline
value class ChannelId private constructor(val value: String) {
    override fun toString(): String = value

    companion object {
        private val pattern = Regex("^[a-z0-9][a-z0-9._-]*(/[a-z0-9][a-z0-9._-]*)*$")

        fun parse(value: String): ChannelId = tryParse(value)
            ?: throw IllegalArgumentException("ChannelId must be a canonical lowercase path")

        fun tryParse(value: String?): ChannelId? = value
            ?.takeIf { it.length <= MAX_IDENTIFIER_LENGTH && pattern.matches(it) }
            ?.let(::ChannelId)
    }
}

enum class TransportKind { Lan, WifiDirect, Bluetooth, Usb }
enum class ConnectivityCapability { Audio, Clipboard }
enum class ChannelKind { Audio, Clipboard }
enum class ChannelDirection { Send, Receive, Duplex }
enum class ConnectivityErrorCode { None, Unavailable, Unauthorized, Timeout, TransportFailure, ProtocolFailure, Cancelled }
enum class SessionState { Idle, Discovering, Connecting, Authenticating, Ready, Streaming, Reconnecting, Closing, Closed, Failed }

data class Peer(val id: PeerId, val displayName: String?, val capabilities: Set<ConnectivityCapability>)
data class Session(val id: SessionId, val peerId: PeerId, val transport: TransportKind, val state: SessionState)
data class Channel(val id: ChannelId, val kind: ChannelKind, val direction: ChannelDirection, val nextSequence: Long = 0)
data class ConnectivityError(val code: ConnectivityErrorCode, val message: String, val isRetryable: Boolean)

object SessionStateMachine {
    private val allowed = mapOf(
        SessionState.Idle to setOf(SessionState.Discovering, SessionState.Connecting, SessionState.Closing, SessionState.Failed),
        SessionState.Discovering to setOf(SessionState.Idle, SessionState.Connecting, SessionState.Closing, SessionState.Failed),
        SessionState.Connecting to setOf(SessionState.Authenticating, SessionState.Reconnecting, SessionState.Closing, SessionState.Failed),
        SessionState.Authenticating to setOf(SessionState.Ready, SessionState.Reconnecting, SessionState.Closing, SessionState.Failed),
        SessionState.Ready to setOf(SessionState.Streaming, SessionState.Reconnecting, SessionState.Closing, SessionState.Failed),
        SessionState.Streaming to setOf(SessionState.Ready, SessionState.Reconnecting, SessionState.Closing, SessionState.Failed),
        SessionState.Reconnecting to setOf(SessionState.Connecting, SessionState.Authenticating, SessionState.Ready, SessionState.Closing, SessionState.Failed),
        SessionState.Closing to setOf(SessionState.Closed, SessionState.Failed),
        SessionState.Failed to setOf(SessionState.Reconnecting, SessionState.Closing, SessionState.Closed),
        SessionState.Closed to emptySet()
    )

    fun canTransition(from: SessionState, to: SessionState): Boolean = from == to || to in allowed.getValue(from)
}
