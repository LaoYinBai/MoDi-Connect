package com.modi.connect.core.connectivity

enum class ChannelRuntimeState { Closed, Open }

data class ChannelDescriptor(val id: ChannelId, val kind: ChannelKind, val direction: ChannelDirection)

object AudioChannel {
    val primary = ChannelId.parse("audio/primary")
    val descriptor = ChannelDescriptor(primary, ChannelKind.Audio, ChannelDirection.Duplex)
}

object ClipboardChannel {
    val primary = ChannelId.parse("clipboard/main")
    val descriptor = ChannelDescriptor(primary, ChannelKind.Clipboard, ChannelDirection.Duplex)
}

interface ChannelDataPlane {
    val sessionId: SessionId
    val descriptor: ChannelDescriptor
    val state: ChannelRuntimeState
    val nextSequence: Long
    var onBytesReceived: ((ByteArray) -> Unit)?
    suspend fun open()
    fun reserveSequence(): Long
    fun resetSequence()
    suspend fun send(payload: ByteArray)
    suspend fun close()
}

class ChannelRouter {
    private val lock = Any()
    private val entries = linkedMapOf<Pair<SessionId, ChannelId>, ChannelDataPlane>()
    val channels: List<ChannelDataPlane> get() = synchronized(lock) { entries.values.toList() }

    fun register(channel: ChannelDataPlane) = synchronized(lock) {
        check(entries.putIfAbsent(channel.sessionId to channel.descriptor.id, channel) == null) {
            "Channel is already registered for this session"
        }
    }

    fun find(sessionId: SessionId, channelId: ChannelId): ChannelDataPlane? =
        synchronized(lock) { entries[sessionId to channelId] }

    suspend fun closeSession(sessionId: SessionId) {
        val matches = synchronized(lock) {
            entries.filterKeys { it.first == sessionId }.values.toList().also { channels ->
                channels.forEach { entries.remove(it.sessionId to it.descriptor.id) }
            }
        }
        matches.forEach { it.close() }
    }
}
