package com.modi.connect.core.connectivity.channel

import com.modi.connect.core.connectivity.AudioChannel
import com.modi.connect.core.connectivity.ChannelDataPlane
import com.modi.connect.core.connectivity.ChannelRuntimeState
import com.modi.connect.core.connectivity.SessionId
import com.modi.connect.core.connectivity.TransportSession
import java.util.concurrent.atomic.AtomicLong

/** Maps audio/primary to an approved legacy transport without inspecting or rewriting bytes. */
class LegacyAudioChannelAdapter(
    override val sessionId: SessionId,
    private val transport: TransportSession,
) : ChannelDataPlane {
    private val sequence = AtomicLong()
    override val descriptor = AudioChannel.descriptor
    override var state = ChannelRuntimeState.Closed
        private set
    override val nextSequence get() = sequence.get()
    override var onBytesReceived: ((ByteArray) -> Unit)? = null
    private var subscribed = false

    override suspend fun open() {
        if (!subscribed) {
            transport.onBytesReceived = { payload -> onBytesReceived?.invoke(payload) }
            subscribed = true
        }
        resetSequence()
        state = ChannelRuntimeState.Open
    }

    override fun reserveSequence(): Long {
        check(state == ChannelRuntimeState.Open) { "Channel is closed" }
        return sequence.getAndIncrement()
    }

    override fun resetSequence() = sequence.set(0)

    override suspend fun send(payload: ByteArray) {
        check(state == ChannelRuntimeState.Open) { "Channel is closed" }
        transport.send(payload)
    }

    override suspend fun close() {
        if (subscribed) { transport.onBytesReceived = null; subscribed = false }
        state = ChannelRuntimeState.Closed
    }
}

object AudioChannelComposition {
    fun isEnabled(readSetting: (String) -> String? = System::getProperty): Boolean =
        readSetting("modi.audio.channel") == "1"
}
