package com.modi.connect.core.connectivity.transport

import com.modi.connect.core.connectivity.TransportCandidate
import com.modi.connect.core.connectivity.TransportDescriptor
import com.modi.connect.core.connectivity.TransportEndpoint
import com.modi.connect.core.connectivity.TransportSession
import com.modi.connect.core.connectivity.TransportSessionState
import com.modi.protocol.ITransport
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/** Temporary bridge from the approved legacy transport binary API to the new app-level port. */
class LegacyTransportSessionAdapter(
    override val descriptor: TransportDescriptor,
    private val legacy: ITransport,
    private val discoverBlock: (suspend () -> List<TransportCandidate>)? = null,
    private val listenBlock: (suspend () -> Unit)? = null,
    private val connectBlock: (suspend (TransportEndpoint) -> Unit)? = null,
) : TransportSession {
    private val gate = Mutex()
    private var disposed = false
    override var state: TransportSessionState = TransportSessionState.Idle
        private set
    override var onBytesReceived: ((ByteArray) -> Unit)? = null

    init {
        legacy.onPacketReceived = { payload -> onBytesReceived?.invoke(payload) }
    }

    override suspend fun discover(): List<TransportCandidate> {
        checkActive()
        if (!descriptor.supportsDiscovery) return emptyList()
        val operation = checkNotNull(discoverBlock) { "Discovery is not bound for ${descriptor.kind}" }
        state = TransportSessionState.Discovering
        return try { operation() } finally {
            if (state == TransportSessionState.Discovering) state = TransportSessionState.Idle
        }
    }

    override suspend fun listen() {
        checkActive()
        check(descriptor.supportsListening && listenBlock != null) { "Listening is not bound for ${descriptor.kind}" }
        state = TransportSessionState.Listening
        listenBlock.invoke()
    }

    override suspend fun connect(endpoint: TransportEndpoint) = gate.withLock {
        checkActive()
        if (state == TransportSessionState.Connected) return@withLock
        state = TransportSessionState.Connecting
        try {
            connectBlock?.invoke(endpoint) ?: legacy.connect()
            state = TransportSessionState.Connected
        } catch (error: Throwable) {
            state = TransportSessionState.Failed
            throw error
        }
    }

    override suspend fun send(payload: ByteArray) {
        checkActive()
        check(state == TransportSessionState.Connected) { "Transport session is not connected" }
        legacy.send(payload)
    }

    override suspend fun close() = gate.withLock {
        if (state == TransportSessionState.Closed || state == TransportSessionState.Idle) {
            state = TransportSessionState.Closed
            return@withLock
        }
        state = TransportSessionState.Closing
        try {
            legacy.disconnect()
            state = TransportSessionState.Closed
        } catch (error: Throwable) {
            state = TransportSessionState.Failed
            throw error
        }
    }

    suspend fun dispose() {
        if (disposed) return
        close()
        legacy.onPacketReceived = null
        disposed = true
    }

    private fun checkActive() = check(!disposed) { "Transport session is disposed" }
}
