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
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.core.TransportIdentity
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.ConcurrentLinkedQueue
import java.util.concurrent.atomic.AtomicBoolean

/**
 * mDNS 设备发现 — 使用 NsdManager 扫描局域网中 _modi._udp 服务
 *
 * ## 线程安全设计
 *
 * NsdManager.resolveService() 不是线程安全的，并发调用会引发 IllegalStateException。
 * 此实现将 resolve 请求**串行化**：
 * 1. onServiceFound 发现的设备加入 pendingResolve 队列（已去重）
 * 2. 通过 tryDequeue() 保证同一时间只有一个 resolve 在跑
 * 3. resolve 完成或失败后继续取下一个，永不并发
 *
 * ## 使用
 * ```kotlin
 * val discovery = MoDiDiscovery(context)
 * discovery.setOnDeviceFound { device -> ... }
 * discovery.startScan()
 * // ...
 * discovery.stopScan()
 * ```
 */
class MoDiDiscovery(
    private val context: Context
) {
    companion object {
        private const val TAG = "MoDiDiscovery"
        const val SERVICE_TYPE = TransportIdentity.MDNS_SERVICE_TYPE
    }

    /** 发现的设备信息 */
    data class DeviceInfo(
        val name: String,       // 电脑名，e.g. "LAPTOP-SILVERWHITE"
        val host: String,       // IP 地址
        val port: Int           // 端口
    )

    private val nsdManager = context.getSystemService(Context.NSD_SERVICE) as NsdManager
    private var isRunning = false

    // ── 线程安全的数据结构 ──
    private val resolved = ConcurrentHashMap<String, DeviceInfo>()             // 已解析完成的设备
    private val pendingOrResolved = ConcurrentHashMap.newKeySet<String>()      // resolve 中/已完成的设备名（去重用）
    private val pendingQueue = ConcurrentLinkedQueue<NsdServiceInfo>()         // 待 resolve 队列（FIFO）
    private val isResolving = AtomicBoolean(false)                             // 当前是否有 resolve 在进行

    // ── 回调（由外部 set 注入） ──
    private var onDeviceFound: ((DeviceInfo) -> Unit)? = null
    private var onDeviceLost: ((DeviceInfo) -> Unit)? = null
    private var onError: ((String) -> Unit)? = null

    fun setOnDeviceFound(cb: (DeviceInfo) -> Unit) { onDeviceFound = cb }
    fun setOnDeviceLost(cb: (DeviceInfo) -> Unit) { onDeviceLost = cb }
    fun setOnError(cb: (String) -> Unit) { onError = cb }

    /** 开始扫描，可重复 start/stop */
    fun startScan() {
        if (isRunning) return
        isRunning = true
        resolved.clear()
        pendingOrResolved.clear()
        pendingQueue.clear()
        isResolving.set(false)

        try {
            nsdManager.discoverServices(
                SERVICE_TYPE,
                NsdManager.PROTOCOL_DNS_SD,
                discoveryListener
            )
        } catch (e: Exception) {
            Log.e(TAG, "discoverServices failed: ${e.message}")
            isRunning = false
            onError?.invoke("启动 mDNS 扫描失败：${e.message}")
        }
    }

    /** 停止扫描并清理 */
    fun stopScan() {
        isRunning = false
        try { nsdManager.stopServiceDiscovery(discoveryListener) } catch (_: Exception) {}
        resolved.clear()
        pendingOrResolved.clear()
        pendingQueue.clear()
        isResolving.set(false)
    }

    // ── NsdManager 发现监听器 ──
    private val discoveryListener = object : NsdManager.DiscoveryListener {
        override fun onDiscoveryStarted(regType: String) {}
        override fun onDiscoveryStopped(serviceType: String) {}
        override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) {
            Log.e(TAG, "discovery start failed: $errorCode")
            onError?.invoke("mDNS 发现启动失败")
        }
        override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) {
            Log.e(TAG, "discovery stop failed: $errorCode")
        }

        // 发现新设备 → 入队列，触发串行 resolve
        override fun onServiceFound(info: NsdServiceInfo) {
            val name = info.serviceName
            if (pendingOrResolved.contains(name)) return  // 已在队列中或已解析，跳过
            pendingOrResolved.add(name)
            pendingQueue.offer(info)
            tryDequeue()
        }

        // 设备消失 → 通知上层
        override fun onServiceLost(info: NsdServiceInfo) {
            val name = info.serviceName
            val device = resolved.remove(name)
            pendingOrResolved.remove(name)
            device?.let { onDeviceLost?.invoke(it) }
        }
    }

    // ── 串行解析器：保证同一时间只有一个 resolveService 在跑 ──

    private fun tryDequeue() {
        if (!isResolving.compareAndSet(false, true)) return  // 已有 resolve 进行中
        val info = pendingQueue.poll() ?: run {
            isResolving.set(false)
            return
        }
        resolveService(info)
    }

    private fun resolveService(info: NsdServiceInfo) {
        nsdManager.resolveService(info, object : NsdManager.ResolveListener {
            override fun onResolveFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
                Log.w(TAG, "resolve ${info.serviceName} failed: $errorCode")
                isResolving.set(false)
                tryDequeue()  // 继续处理下一个
            }

            override fun onServiceResolved(resolvedInfo: NsdServiceInfo) {
                val name = resolvedInfo.serviceName
                val host = resolvedInfo.host?.hostAddress ?: ""
                val port = resolvedInfo.port
                if (host.isNotEmpty()) {
                    resolved[name] = DeviceInfo(name, host, port)
                    onDeviceFound?.invoke(DeviceInfo(name, host, port))
                }
                isResolving.set(false)
                tryDequeue()  // 继续处理下一个
            }
        })
    }
}
