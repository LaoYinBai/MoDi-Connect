package com.modi.connect.core.connectivity

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

enum class BuiltInModuleState { Unavailable, Ready, Failed, Closed }

data class BuiltInModuleDescriptor(
    val id: String,
    val capability: ConnectivityCapability,
    val channel: ChannelDescriptor,
)

data class BuiltInModuleSnapshot(
    val sessionId: SessionId,
    val moduleId: String,
    val capability: ConnectivityCapability,
    val state: BuiltInModuleState,
    val failureReason: String? = null,
)

data class BuiltInModuleResult(
    val succeeded: Boolean,
    val state: BuiltInModuleState,
    val failureReason: String? = null,
)

object BuiltInModules {
    val audio = BuiltInModuleDescriptor("audio", ConnectivityCapability.Audio, AudioChannel.descriptor)
    val clipboard = BuiltInModuleDescriptor("clipboard", ConnectivityCapability.Clipboard, ClipboardChannel.descriptor)

    fun audioModule(
        handle: suspend (SessionId, ByteArray) -> Unit,
        start: suspend (SessionId, ChannelDataPlane) -> Unit = { _, _ -> },
        stop: suspend (SessionId) -> Unit = {},
    ): BuiltInModule = ChannelBoundBuiltInModule(audio, handle, start, stop)

    fun clipboardModule(
        handle: suspend (SessionId, ByteArray) -> Unit,
        start: suspend (SessionId, ChannelDataPlane) -> Unit = { _, _ -> },
        stop: suspend (SessionId) -> Unit = {},
    ): BuiltInModule = ChannelBoundBuiltInModule(clipboard, handle, start, stop)

    private class ChannelBoundBuiltInModule(
        override val descriptor: BuiltInModuleDescriptor,
        private val handlePayload: suspend (SessionId, ByteArray) -> Unit,
        private val startModule: suspend (SessionId, ChannelDataPlane) -> Unit,
        private val stopModule: suspend (SessionId) -> Unit,
    ) : BuiltInModule {
        override suspend fun start(sessionId: SessionId, channel: ChannelDataPlane) = startModule(sessionId, channel)
        override suspend fun handle(sessionId: SessionId, payload: ByteArray) = handlePayload(sessionId, payload)
        override suspend fun stop(sessionId: SessionId) = stopModule(sessionId)
    }
}

interface BuiltInModule {
    val descriptor: BuiltInModuleDescriptor
    suspend fun start(sessionId: SessionId, channel: ChannelDataPlane)
    suspend fun handle(sessionId: SessionId, payload: ByteArray)
    suspend fun stop(sessionId: SessionId)
}

/** Compile-time built-ins only. No class loading, scripts or remote code are accepted. */
class BuiltInModuleRegistry(modules: List<BuiltInModule>) {
    private data class ActiveModule(val module: BuiltInModule, val channel: ChannelDataPlane)

    private val lock = Mutex()
    private val modulesById: Map<String, BuiltInModule>
    private val active = linkedMapOf<Pair<SessionId, String>, ActiveModule>()
    private val mutableSnapshots = linkedMapOf<Pair<SessionId, String>, BuiltInModuleSnapshot>()
    @Volatile private var snapshotView = emptyList<BuiltInModuleSnapshot>()

    init {
        require(modules.all { it.descriptor.id.isNotBlank() }) { "Built-in module IDs must be non-empty" }
        require(modules.map { it.descriptor.id }.distinct().size == modules.size) { "Built-in module IDs must be unique" }
        modulesById = modules.associateBy { it.descriptor.id }
    }

    val registeredCapabilities = modules.map { it.descriptor.capability }.distinct().sortedBy { it.ordinal }
    val snapshots: List<BuiltInModuleSnapshot> get() = snapshotView

    suspend fun activate(
        sessionId: SessionId,
        moduleId: String,
        channel: ChannelDataPlane,
    ): BuiltInModuleResult {
        val module = modulesById[moduleId]
            ?: return BuiltInModuleResult(false, BuiltInModuleState.Unavailable, "Built-in module is not registered")
        if (channel.sessionId != sessionId || channel.descriptor != module.descriptor.channel) {
            return BuiltInModuleResult(false, BuiltInModuleState.Unavailable, "Channel does not match the built-in module")
        }

        return lock.withLock {
            val key = sessionId to moduleId
            if (key in active) {
                val current = mutableSnapshots.getValue(key)
                return@withLock BuiltInModuleResult(false, current.state, "Built-in module is already active")
            }
            try {
                channel.open()
                module.start(sessionId, channel)
                active[key] = ActiveModule(module, channel)
                setSnapshot(sessionId, module.descriptor, BuiltInModuleState.Ready)
                BuiltInModuleResult(true, BuiltInModuleState.Ready)
            } catch (error: CancellationException) {
                runCatching { channel.close() }
                throw error
            } catch (error: Exception) {
                runCatching { channel.close() }
                setSnapshot(sessionId, module.descriptor, BuiltInModuleState.Failed, error.javaClass.simpleName)
                BuiltInModuleResult(false, BuiltInModuleState.Failed, error.javaClass.simpleName)
            }
        }
    }

    suspend fun dispatch(sessionId: SessionId, moduleId: String, payload: ByteArray): BuiltInModuleResult =
        lock.withLock {
            val key = sessionId to moduleId
            val current = active[key]
                ?: return@withLock BuiltInModuleResult(false, BuiltInModuleState.Unavailable, "Built-in module is not active")
            val snapshot = mutableSnapshots.getValue(key)
            if (snapshot.state != BuiltInModuleState.Ready) {
                return@withLock BuiltInModuleResult(false, snapshot.state, snapshot.failureReason)
            }
            try {
                current.module.handle(sessionId, payload)
                BuiltInModuleResult(true, BuiltInModuleState.Ready)
            } catch (error: CancellationException) {
                throw error
            } catch (error: Exception) {
                setSnapshot(sessionId, current.module.descriptor, BuiltInModuleState.Failed, error.javaClass.simpleName)
                BuiltInModuleResult(false, BuiltInModuleState.Failed, error.javaClass.simpleName)
            }
        }

    suspend fun stopSession(sessionId: SessionId) = lock.withLock {
        active.filterKeys { it.first == sessionId }.toMap().forEach { (key, current) ->
            runCatching { current.module.stop(sessionId) }
            runCatching { current.channel.close() }
            setSnapshot(sessionId, current.module.descriptor, BuiltInModuleState.Closed)
            active.remove(key)
        }
    }

    private fun setSnapshot(
        sessionId: SessionId,
        descriptor: BuiltInModuleDescriptor,
        state: BuiltInModuleState,
        failureReason: String? = null,
    ) {
        mutableSnapshots[sessionId to descriptor.id] = BuiltInModuleSnapshot(
            sessionId, descriptor.id, descriptor.capability, state, failureReason
        )
        snapshotView = mutableSnapshots.values.sortedWith(compareBy({ it.sessionId.value }, { it.moduleId }))
    }
}
