package com.modi.connect.core.connectivity.legacy

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
import java.util.UUID

/** Shared lifecycle for a physical legacy connection already authenticated by its owning link. */
open class AuthenticatedLegacySessionRuntime(
    sessionUuid: UUID,
    peerId: PeerId,
    kind: TransportKind,
    private val endpoint: TransportEndpoint,
    legacy: ITransport,
) {
    private val id = SessionId.parse(sessionUuid.toString().replace("-", ""))
    private val transport = LegacyTransportSessionAdapter(TransportDescriptor.forKind(kind), legacy)
    val sessions = SessionRegistry()
    val channels = ChannelRouter()
    val peer = Peer(peerId, null, setOf(ConnectivityCapability.Audio))
    var session = Session(id, peer.id, kind, SessionState.Connecting)
        private set
    lateinit var audioChannel: ChannelDataPlane
        private set

    suspend fun connect() {
        sessions.observeStarted(id, session.transport, SessionState.Connecting)
        transport.connect(endpoint)
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
}
