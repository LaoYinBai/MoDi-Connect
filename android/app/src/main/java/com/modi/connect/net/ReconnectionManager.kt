/*
 * MoDi Connect - Cross-device interconnection protocol
 * Copyright (C) 2026 Silvite
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
package com.modi.connect.net

import android.content.Context
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.core.interfaces.INetworkMonitor
import com.modi.connect.ConnectionState
import com.modi.connect.ConnectionStateManager
import com.modi.connect.audio.AudioPipeline
import kotlinx.coroutines.*

/**
 * 断线重连管理器 — 旁路观测层
 *
 * ## 职责
 * 1. 监听 WiFi 状态变化（ConnectivityManager.NetworkCallback）
 * 2. 进入 RECONNECTING 后自动执行重连循环
 * 3. 重连循环：优先最后 IP → mDNS → HELLO → startStreaming → retry × 5
 *
 * ## 约束
 * - 不直接操作 AudioPipeline 内部，通过 [onRecover] 回调复用已有握手/启动流程
 * - 不操作 UI，只通过 [stateManager] 通知状态变化
 * - stopStreaming 时只释放 capture/encoder/sender，保留 lastKnownHost/stateManager/pipeline
 *
 * ## 触发源（五路）
 * 1. WiFi 断连 — NetworkCallback.onLost
 * 2. HELLO 失败 — doHandshake 返回 false
 * 3. Socket 异常 — UdpTransport 或握手 Transport 异常
 * 4. 音频发送异常 — AudioPipeline 发送失败
 * 5. 用户手动重连 — UI 按钮
 */
class ReconnectionManager(
    private val context: Context,
    private val stateManager: ConnectionStateManager,
    private val pipeline: AudioPipeline,
    /** 网络状态监听（由 PlatformFactory 创建注入） */
    private val networkMonitor: INetworkMonitor,
    /** 恢复回调，由 MainActivity 注入，复用已有 doHandshake + pipe.startStreaming */
    private val onRecover: suspend (host: String, mode: Int) -> Boolean
) {
    companion object {
        private const val TAG = "ReconnectionManager"
        private const val MAX_RETRIES = 5           // 最大重试次数
        private const val RETRY_INTERVAL_MS = 2000L  // 重试间隔（毫秒）
        private const val MDNS_TIMEOUT_MS = 3000L    // mDNS 扫描超时（毫秒）
    }

    // ── 最后成功连接的主机信息（由 MainActivity 在握手成功后设置） ──
    @Volatile var lastKnownHost: String? = null
    @Volatile var lastRouteMode: Int = 0

    // ── 内部状态 ──
    // scope 在 start() 时创建；stop() 取消后置空，start() 重建。
    // 否则 Activity 重建（close→start）后 scope 已被永久取消，重连循环静默死亡。
    private var scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    @Volatile private var recovering = false

    // ── 网络状态监听（通过 INetworkMonitor 接口） ──
    private val networkChangedHandler: (com.modi.connect.core.models.NetworkInfo) -> Unit = { info ->
        if (!info.isConnected) {
            // 网络断开：只在 STREAMING 中才进入 RECONNECTING
            if (stateManager.state != ConnectionState.STREAMING) {
                Log.i(TAG, "网络断开，但当前状态为 ${stateManager.state}，不触发重连")
            } else {
                Log.i(TAG, "网络断开 → RECONNECTING")
                stateManager.update(ConnectionState.RECONNECTING)
            }
        } else {
            // 网络恢复：如果当前是 RECONNECTING 且未在重连中，启动重连
            Log.i(TAG, "网络恢复")
            if (stateManager.state == ConnectionState.RECONNECTING && !recovering) {
                startRecovery()
            }
        }
    }

    // ── 生命周期 ──

    /** 开始监听网络状态变化（通过 INetworkMonitor）。可重入：stop 后再次 start 会重建协程作用域。 */
    fun start() {
        scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        networkMonitor.onNetworkChanged = networkChangedHandler
        networkMonitor.start()
        Log.i(TAG, "ReconnectionManager started")
    }

    /** 停止监听，取消所有重连任务 */
    fun stop() {
        networkMonitor.stop()
        networkMonitor.onNetworkChanged = null
        scope.cancel()
        recovering = false
        Log.i(TAG, "ReconnectionManager stopped")
    }

    /** 取消当前重连（用户手动停止时调用） */
    fun cancelRecovery() {
        recovering = false
        scope.coroutineContext[Job]?.children?.forEach { it.cancel() }
        Log.i(TAG, "Recovery cancelled by user")
    }

    // ── 五路触发入口 ──

    /**
     * 外部触发重连（HELLO 失败 / Socket 异常 / 音频发送异常 / 用户手动）
     *
     * 调用方需先调用 stateManager.update(RECONNECTING)
     * 此方法立即启动重连循环（不走 WiFi 恢复等待）
     */
    fun triggerRecovery() {
        // 调用方已设置 RECONNECTING，无需重复设置
        if (recovering) {
            Log.i(TAG, "Already recovering, ignoring trigger")
            return
        }
        startRecovery()
    }

    // ── 重连循环 ──

    private fun startRecovery() {
        recovering = true
        scope.launch {
            try {
                // 快照：防止重连过程中被外部修改
                val host = lastKnownHost
                val route = lastRouteMode

                // 停止当前推流（只释放 capture/encoder/sender，保留 lastKnownHost/stateManager/pipeline）
                pipeline.stopStreaming()
                Log.i(TAG, "推流已停止，准备重连（host=$host, route=$route）")

                if (host == null) {
                    Log.w(TAG, "没有已知主机，直接进入 ERROR")
                    stateManager.update(ConnectionState.ERROR)
                    return@launch
                }

                // 5 次重试循环
                for (attempt in 1..MAX_RETRIES) {
                    Log.i(TAG, "重连尝试 $attempt/$MAX_RETRIES")

                    val ok = if (attempt == 1) {
                        // 尝试 1: 直接 HELLO 最后一次成功 IP（最快路径）
                        withContext(Dispatchers.IO) { performRecover(host, route) }
                    } else {
                        // 尝试 2~5: mDNS 扫描 + HELLO
                        val found = withContext(Dispatchers.IO) { scanMdns() }
                        if (found != null) {
                            withContext(Dispatchers.IO) { performRecover(found, route) }.also {
                                if (it) lastKnownHost = found
                            }
                        } else false
                    }

                    if (ok) {
                        Log.i(TAG, "重连成功！（第 $attempt 次尝试）")
                        stateManager.update(ConnectionState.STREAMING)
                        return@launch
                    }

                    Log.w(TAG, "第 $attempt 次尝试失败")
                    if (attempt < MAX_RETRIES) {
                        delay(RETRY_INTERVAL_MS)
                    }
                }

                // 5 次全失败
                Log.e(TAG, "5 次重试均失败")
                stateManager.update(ConnectionState.ERROR)
            } finally {
                recovering = false
            }
        }
    }

    // ── 单次恢复尝试 ──

    /**
     * 执行一次完整恢复：HELLO + startStreaming
     * 复用 onRecover 回调（MainActivity 注入，内容为 doHandshake + pipe.startStreaming）
     */
    private suspend fun performRecover(host: String, route: Int): Boolean {
        return try {
            onRecover(host, route)
        } catch (e: Exception) {
            Log.e(TAG, "恢复尝试异常: ${e.message}")
            false
        }
    }

    // ── mDNS 短暂扫描 ──

    /**
     * 临时扫描 mDNS，取第一个发现的设备 IP
     * 通过 IDiscovery 接口（而非直接调用 MoDiDiscovery）
     * 超时 [MDNS_TIMEOUT_MS] 返回 null
     */
    private suspend fun scanMdns(): String? {
        val discovery = com.modi.connect.core.factory.PlatformFactory.createDiscovery(context)
        var foundHost: String? = null
        val latch = CompletableDeferred<Unit>()

        discovery.onDeviceFound = { device ->
            if (foundHost == null) {
                foundHost = device.ip
                latch.complete(Unit)
            }
        }

        discovery.start()

        try {
            withTimeout(MDNS_TIMEOUT_MS) { latch.await() }
        } catch (_: TimeoutCancellationException) {
            Log.w(TAG, "mDNS 扫描超时（${MDNS_TIMEOUT_MS}ms）")
        } catch (_: CancellationException) {
            // 协程取消，不处理
        }

        discovery.stop()
        return foundHost
    }
}
