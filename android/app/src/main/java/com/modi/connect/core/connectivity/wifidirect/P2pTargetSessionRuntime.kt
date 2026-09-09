package com.modi.connect.core.connectivity.wifidirect

import com.modi.connect.core.TransportIdentity
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
import java.security.MessageDigest
import java.util.UUID

/** Dev-gated P2P owner entered only after the existing QR/trust gate and HELLO authentication. */
class P2pTargetSessionRuntime(
    sessionUuid: UUID,
    peerCredential: String,
    private val host: String,
    private val localBindAddress: String?,
    createTransport: (String, String?) -> ITransport = { target, bindAddress ->
        PlatformFactory.createTransport(
            TransportType.Udp,
            target,
            TransportIdentity.AUDIO_PORT,
            localBindAddress = bindAddress,
        )
    },
) {
    private val trustedPeerId = peerCredential
        .also { require(it.isNotBlank()) { "An authenticated Wi-Fi Direct peer credential is required" } }
        .let { PeerId.parse("wifi-direct:${sha256(it)}") }
    private val id = SessionId.parse(sessionUuid.toString().replace("-", ""))
    private val transport = LegacyTransportSessionAdapter(
        TransportDescriptor.forKind(TransportKind.WifiDirect),
        createTransport(host, localBindAddress),
    )
    val sessions = SessionRegistry()
    val channels = ChannelRouter()
    val peer = Peer(
        trustedPeerId,
        null,
        setOf(ConnectivityCapability.Audio),
    )
    var session = Session(id, peer.id, TransportKind.WifiDirect, SessionState.Connecting)
        private set
    lateinit var audioChannel: ChannelDataPlane
        private set

    suspend fun connect() {
        sessions.observeStarted(id, TransportKind.WifiDirect, SessionState.Connecting)
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
        fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
            .digest(value.toByteArray(Charsets.UTF_8))
            .joinToString("") { "%02x".format(it) }
    }
}

object P2pTargetComposition {
    fun isEnabled(buildFlag: Boolean): Boolean = buildFlag
}
