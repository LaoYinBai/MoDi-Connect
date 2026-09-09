package com.modi.connect.core.connectivity.lan

import com.modi.connect.core.TransportIdentity
import com.modi.connect.core.connectivity.AudioChannel
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
import com.modi.connect.core.factory.PlatformFactory
import com.modi.protocol.ITransport
import com.modi.protocol.TransportType
import java.util.UUID

/** Dev-gated LAN connection owner: Peer -> Session -> Transport -> audio/primary Channel. */
class LanTargetSessionRuntime(
    sessionUuid: UUID,
    peerKey: String?,
    private val host: String,
    createTransport: (String) -> ITransport = { target ->
        PlatformFactory.createTransport(TransportType.Udp, target, TransportIdentity.AUDIO_PORT)
    },
) {
    private val id = SessionId.parse(sessionUuid.toString().replace("-", ""))
    private val transport = LegacyTransportSessionAdapter(
        TransportDescriptor.forKind(TransportKind.Lan),
        createTransport(host),
    )
    val sessions = SessionRegistry()
    val channels = ChannelRouter()
    val peer = Peer(
        PeerId.parse("lan:${normalizedPeerKey(peerKey, host)}"),
        peerKey,
        setOf(ConnectivityCapability.Audio),
    )
    var session = Session(id, peer.id, TransportKind.Lan, SessionState.Connecting)
        private set
    lateinit var audioChannel: ChannelDataPlane
        private set

    suspend fun connect() {
        sessions.observeStarted(id, TransportKind.Lan, SessionState.Connecting)
        transport.connect(TransportEndpoint.parse("udp://$host:${TransportIdentity.AUDIO_PORT}"))
        observe(SessionState.Authenticating)
        audioChannel = LegacyAudioChannelAdapter(id, transport)
        channels.register(audioChannel)
        observe(SessionState.Ready)
    }

    fun observeStreaming() = observe(SessionState.Streaming)
    fun observeReconnecting() = observe(SessionState.Reconnecting)

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
        fun normalizedPeerKey(peerKey: String?, host: String): String =
            (peerKey?.takeIf { it.isNotBlank() } ?: host).trim().take(120)
    }
}

object LanTargetComposition {
    fun isEnabled(buildFlag: Boolean): Boolean = buildFlag
}
