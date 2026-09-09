package com.modi.connect.core.connectivity

enum class TransportSessionState {
    Idle,
    Discovering,
    Listening,
    Connecting,
    Connected,
    Closing,
    Closed,
    Failed,
}

data class TransportDescriptor(
    val kind: TransportKind,
    val supportsDiscovery: Boolean,
    val supportsListening: Boolean,
    val supportsConnecting: Boolean,
    val isOptional: Boolean,
) {
    companion object {
        fun forKind(kind: TransportKind): TransportDescriptor = when (kind) {
            TransportKind.Lan -> TransportDescriptor(kind, true, true, true, false)
            TransportKind.WifiDirect -> TransportDescriptor(kind, true, true, true, true)
            TransportKind.Bluetooth -> TransportDescriptor(kind, false, true, true, true)
            TransportKind.Usb -> TransportDescriptor(kind, false, true, true, true)
        }
    }
}

@JvmInline
value class TransportEndpoint private constructor(val value: String) {
    override fun toString(): String = value

    companion object {
        fun parse(value: String): TransportEndpoint {
            require(value.isNotBlank() && value == value.trim() && value.length <= 512) {
                "Transport endpoint must be a non-empty opaque identifier"
            }
            return TransportEndpoint(value)
        }
    }
}

data class TransportCandidate(
    val endpoint: TransportEndpoint,
    val displayName: String?,
    val confirmedPeerId: PeerId? = null,
)

interface TransportSession {
    val descriptor: TransportDescriptor
    val state: TransportSessionState
    var onBytesReceived: ((ByteArray) -> Unit)?
    suspend fun discover(): List<TransportCandidate>
    suspend fun listen()
    suspend fun connect(endpoint: TransportEndpoint)
    suspend fun send(payload: ByteArray)
    suspend fun close()
}

class TransportUnavailableException(kind: TransportKind) :
    IllegalStateException("Transport $kind is not registered")

class TransportRegistry {
    private val factories = linkedMapOf<TransportKind, () -> TransportSession>()
    val registeredKinds: Set<TransportKind> get() = factories.keys.toSet()

    fun register(kind: TransportKind, factory: () -> TransportSession) {
        check(kind !in factories) { "Transport $kind is already registered" }
        factories[kind] = factory
    }

    fun tryCreate(kind: TransportKind): Result<TransportSession> = runCatching {
        val factory = factories[kind] ?: throw TransportUnavailableException(kind)
        factory()
    }
}
