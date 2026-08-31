/* MoDi Connect — Copyright (C) 2026 Silvite. GPL-3.0-or-later. */
package com.modi.connect.net

import com.modi.connect.ConnectionState
import com.modi.connect.ConnectionStateManager
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.core.interfaces.INetworkMonitor
import kotlinx.coroutines.*

/** Owns LAN recovery. Never discovers/selects a different peer on the user's behalf. */
class ReconnectionManager(
    private val stateManager: ConnectionStateManager,
    private val networkMonitor: INetworkMonitor,
    private val stopStreaming: suspend () -> Unit,
    private val onRecover: suspend (host: String, mode: Int) -> Boolean,
    private val dispatcher: CoroutineDispatcher = Dispatchers.IO,
) {
    private val gate = Any()
    private var scope = CoroutineScope(dispatcher + SupervisorJob())
    private var recovery: Job? = null
    private var host: String? = null
    private var route = 0
    private var started = false

    fun arm(host: String, route: Int) = synchronized(gate) {
        check(recovery?.isActive != true) { "Cancel previous recovery before arming a session" }
        this.host = host
        this.route = route
    }

    fun updateRoute(route: Int) = synchronized(gate) { this.route = route }

    fun start() = synchronized(gate) {
        if (started) return@synchronized
        if (!scope.isActive) scope = CoroutineScope(dispatcher + SupervisorJob())
        started = true
        networkMonitor.onNetworkChanged = { info ->
            if (synchronized(gate) { host != null }) {
                if (!info.isConnected && stateManager.state == ConnectionState.STREAMING)
                    stateManager.update(ConnectionState.RECONNECTING)
                else if (info.isConnected && stateManager.state == ConnectionState.RECONNECTING)
                    triggerRecovery()
            }
        }
        networkMonitor.start()
    }

    fun stop() = synchronized(gate) {
        host = null
        started = false
        networkMonitor.onNetworkChanged = null
        networkMonitor.stop()
        scope.cancel()
    }

    /** Disarm before cancellation, and join before another link may use the shared pipeline. */
    suspend fun cancelRecovery() {
        val previous = synchronized(gate) {
            host = null
            recovery.also { it?.cancel() }
        }
        withContext(NonCancellable) { previous?.join() }
        synchronized(gate) { if (recovery === previous) recovery = null }
    }

    fun triggerRecovery() = synchronized(gate) {
        val target = host ?: return@synchronized
        if (recovery?.isActive == true) return@synchronized
        recovery = scope.launch(start = CoroutineStart.LAZY) {
            try {
                stopStreaming()
                for (attempt in 1..5) {
                    ensureActive()
                    val currentRoute = synchronized(gate) { route }
                    val ok = try { onRecover(target, currentRoute) }
                    catch (cancelled: CancellationException) { throw cancelled }
                    catch (error: Exception) {
                        Log.w("ReconnectionManager", "恢复失败: ${error.message}")
                        false
                    }
                    ensureActive()
                    if (ok) {
                        stateManager.update(ConnectionState.STREAMING)
                        return@launch
                    }
                    if (attempt < 5) delay(2000)
                }
                stateManager.update(ConnectionState.ERROR, "原电脑无法恢复，请重新选择电脑或授权录音")
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (error: Exception) {
                Log.w("ReconnectionManager", "停止旧推流失败: ${error.message}")
                stateManager.update(ConnectionState.ERROR, "连接恢复失败，请重新连接")
            }
        }.also { it.start() }
    }
}
