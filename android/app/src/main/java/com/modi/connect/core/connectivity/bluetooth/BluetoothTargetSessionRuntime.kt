package com.modi.connect.core.connectivity.bluetooth

import com.modi.connect.core.connectivity.ChannelDataPlane
import com.modi.connect.core.connectivity.ChannelRouter
import com.modi.connect.core.connectivity.ConnectivityCapability
import com.modi.connect.core.connectivity.Peer
import com.modi.connect.core.connectivity.PeerId
import com.modi.connect.core.connectivity.Session
import com.modi.connect.core.connectivity.SessionId
import com.modi.connect.core.connectivity.SessionRegistry
import com.modi.connect.core.connectivity.SessionState
import com.modi.connect.core.connectivity.TransportDescriptor
import com.modi.connect.core.connectivity.TransportEndpoint
import com.modi.connect.core.connectivity.TransportKind
import com.modi.connect.core.connectivity.channel.LegacyAudioChannelAdapter
import com.modi.connect.core.connectivity.transport.LegacyTransportSessionAdapter
import com.modi.protocol.ITransport
import java.security.MessageDigest
import java.util.UUID

/** Dev-gated projection entered only after bonded-device connection and HELLO authentication. */
class BluetoothTargetSessionRuntime(
    sessionUuid: UUID,
    peerCredential: String,
    legacy: ITransport,
) {
    private val peerId = peerCredential
        .also { require(it.isNotBlank()) { "A bonded Bluetooth peer identity is required" } }
        .let { PeerId.parse("bluetooth:${sha256(it)}") }
    private val id = SessionId.parse(sessionUuid.toString().replace("-", ""))
    private val transport = LegacyTransportSessionAdapter(
        TransportDescriptor.forKind(TransportKind.Bluetooth),
        legacy,
    )
    val sessions = SessionRegistry()
    val channels = ChannelRouter()
    val peer = Peer(peerId, null, setOf(ConnectivityCapability.Audio))
    var session = Session(id, peer.id, TransportKind.Bluetooth, SessionState.Connecting)
        private set
    lateinit var audioChannel: ChannelDataPlane
        private set

    suspend fun connect() {
        sessions.observeStarted(id, TransportKind.Bluetooth, SessionState.Connecting)
        transport.connect(TransportEndpoint.parse("rfcomm://bonded-peer"))
        observe(SessionState.Authenticating)
        audioChannel = LegacyAudioChannelAdapter(id, transport)
        channels.register(audioChannel)
        observe(SessionState.Ready)
    }

    fun observeStreaming() = observe(SessionState.Streaming)

    suspend fun close() {
        if (session.state == SessionState.Closed) return
        if (session.state != SessionState.Closing) observe(SessionState.Closing)
        channels.closeSession(id)
        transport.dispose()
        observe(SessionState.Closed)
    }

    private fun observe(state: SessionState) {
        val result = sessions.observeState(id, state)
        if (result.applied) session = session.copy(state = state)
    }

    private companion object {
        fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
            .digest(value.toByteArray(Charsets.UTF_8))
            .joinToString("") { "%02x".format(it) }
    }
}

object BluetoothTargetComposition {
    fun isEnabled(buildFlag: Boolean): Boolean = buildFlag
}
