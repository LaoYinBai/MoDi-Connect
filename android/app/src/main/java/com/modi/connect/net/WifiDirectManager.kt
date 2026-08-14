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

import android.Manifest
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.net.wifi.p2p.WifiP2pInfo
import android.net.wifi.p2p.WifiP2pManager
import android.os.Build
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import com.modi.connect.core.infrastructure.Log
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull
import java.net.Inet4Address
import java.net.NetworkInterface
import kotlin.coroutines.resume

/**
 * WifiDirectManager — WiFi Direct P2P 连接管理器（Android 端）
 *
 * ## 核心特性：双网共存
 * 使用 WifiP2pManager.createGroup() 创建 P2P Group（Android 做 GO），
 * 创建独立 P2P 虚拟接口，**不会断开手机当前 WiFi 连接**。
 *
 * ## 职责
 * 1. 接收 QR 解析结果（device name + token）
 * 2. createGroup() → Android 做 GO（自带 DHCP，Windows 客户端自动获取 IP）
 * 3. 监听 CONNECTION_CHANGED → 获取 GO IP（Android 自己的 P2P IP）
 * 4. 等待 Windows 连接（轮询 groupInfo 检测客户端）
 * 5. 返回 GO IP 供 UDP 通信（Windows 向此 IP 发 HELLO）
 */
class WifiDirectManager(private val context: Context) {

    companion object {
        private const val TAG = "WifiDirectManager"
        private const val GROUP_TIMEOUT_MS = 15_000L
        private const val CLIENT_WAIT_TIMEOUT_MS = 60_000L
        private const val P2P_PERMISSION_REQUEST_CODE = 9001

        fun requiredPermission(): String =
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU)
                Manifest.permission.NEARBY_WIFI_DEVICES
            else
                Manifest.permission.ACCESS_FINE_LOCATION
    }

    private val p2pManager =
        context.getSystemService(Context.WIFI_P2P_SERVICE) as WifiP2pManager
    private val channel = p2pManager.initialize(context, context.mainLooper, null)

    /** 进度状态（UI 订阅） */
    private val _statusFlow = MutableStateFlow("空闲")
    val statusFlow: StateFlow<String> = _statusFlow

    /**
     * 创建 P2P Group（Android 做 GO）并等待 Windows 连接
     *
     * @return GO IP（Android P2P IP，如 192.168.49.1），失败返回 null
     */
    suspend fun createGroupAndWaitForClient(): String? =
        withContext(Dispatchers.Main) {
            try {
                // 0. 检查权限
                val perm = requiredPermission()
                if (ContextCompat.checkSelfPermission(context, perm) != PackageManager.PERMISSION_GRANTED) {
                    _statusFlow.value = "请授予附近设备权限后重试"
                    val activity = context as? android.app.Activity
                    if (activity != null) {
                        ActivityCompat.requestPermissions(activity, arrayOf(perm), P2P_PERMISSION_REQUEST_CODE)
                        kotlinx.coroutines.delay(2000)
                    }
                    if (ContextCompat.checkSelfPermission(context, perm) != PackageManager.PERMISSION_GRANTED) {
                        _statusFlow.value = "缺少附近设备权限"
                        return@withContext null
                    }
                }

                // 1. 创建 P2P Group（Android 做 GO）
                _statusFlow.value = "正在创建 P2P Group..."
                Log.i(TAG, "Creating P2P group (Android as GO)...")

                val goIp = withTimeoutOrNull(GROUP_TIMEOUT_MS) { createGroupAndGetIp() }
                if (goIp == null) {
                    _statusFlow.value = "P2P Group 创建失败"
                    return@withContext null
                }

                Log.i(TAG, "P2P Group created, GO IP=$goIp")
                _statusFlow.value = "P2P ✓ go=$goIp 等待电脑连接..."

                // 2. 等待 Windows 连接到 Group
                val clientConnected = withTimeoutOrNull(CLIENT_WAIT_TIMEOUT_MS) {
                    waitForClient()
                }

                if (clientConnected == true) {
                    _statusFlow.value = "P2P ✓ go=$goIp 电脑已连接"
                    Log.i(TAG, "Windows client connected to P2P group")
                } else {
                    _statusFlow.value = "P2P ✓ go=$goIp 等待超时(电脑未连接)"
                    Log.w(TAG, "No client connected within timeout")
                }

                // 无论是否检测到客户端，都返回 GO IP（Windows 可能已连接但未检测到）
                goIp
            } catch (cancelled: CancellationException) {
                _statusFlow.value = "P2P 已取消"
                throw cancelled
            } catch (e: Exception) {
                _statusFlow.value = "P2P 失败：${e.message}"
                Log.e(TAG, "createGroup error: ${e.message}")
                null
            }
        }

    /**
     * 创建 P2P Group 并获取 GO IP
     */
    private suspend fun createGroupAndGetIp(): String? {
        val receiver = P2pBroadcastReceiver()
        val filter = IntentFilter(WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            context.registerReceiver(receiver, filter, Context.RECEIVER_NOT_EXPORTED)
        } else {
            @Suppress("UnspecifiedRegisterReceiverFlag")
            context.registerReceiver(receiver, filter)
        }

        try {
            return suspendCancellableCoroutine { cont ->
                p2pManager.createGroup(channel, object : WifiP2pManager.ActionListener {
                    override fun onSuccess() {
                        Log.i(TAG, "createGroup initiated")
                    }
                    override fun onFailure(reason: Int) {
                        Log.w(TAG, "createGroup failed: reason=$reason")
                        // reason=2 表示 Group 已存在，尝试获取信息
                        if (reason == 2) {
                            p2pManager.requestConnectionInfo(channel) { info ->
                                if (info != null && info.groupFormed && info.isGroupOwner && info.groupOwnerAddress != null) {
                                    if (cont.isActive) cont.resume(info.groupOwnerAddress.hostAddress)
                                } else if (cont.isActive) cont.resume(null)
                            }
                        } else {
                            if (cont.isActive) cont.resume(null)
                        }
                    }
                })

                receiver.onConnectionChanged = {
                    p2pManager.requestConnectionInfo(channel) { info: WifiP2pInfo? ->
                        if (info != null && info.groupFormed && info.isGroupOwner && info.groupOwnerAddress != null) {
                            val goIp = info.groupOwnerAddress.hostAddress
                            Log.i(TAG, "Group formed, GO IP=$goIp")
                            if (cont.isActive) cont.resume(goIp)
                        }
                    }
                }

                cont.invokeOnCancellation {
                    p2pManager.removeGroup(channel, null)
                }
            }
        } finally {
            context.unregisterReceiver(receiver)
        }
    }

    /**
     * 等待 Windows 客户端连接到 Group（轮询 groupInfo 检测客户端）
     */
    private suspend fun waitForClient(): Boolean {
        repeat(60) { attempt ->  // 60 * 1s = 60s
            val hasClient = checkForClients()
            if (hasClient) return true
            if (attempt % 5 == 0) Log.i(TAG, "Waiting for client... ${attempt + 1}s")
            kotlinx.coroutines.delay(1000)
        }
        return false
    }

    private suspend fun checkForClients(): Boolean = suspendCancellableCoroutine { cont ->
        p2pManager.requestGroupInfo(channel) { group ->
            if (group != null && group.clientList.isNotEmpty()) {
                Log.i(TAG, "Client found: ${group.clientList.map { it.deviceName }}")
                if (cont.isActive) cont.resume(true)
            } else {
                if (cont.isActive) cont.resume(false)
            }
        }
    }

    /**
     * 断开 P2P 连接
     */
    fun disconnect() {
        p2pManager.removeGroup(channel, object : WifiP2pManager.ActionListener {
            override fun onSuccess() { Log.i(TAG, "P2P group removed") }
            override fun onFailure(reason: Int) { Log.w(TAG, "removeGroup failed: $reason") }
        })
        _statusFlow.value = "P2P 已断开"
    }

    private class P2pBroadcastReceiver : BroadcastReceiver() {
        var onConnectionChanged: (() -> Unit)? = null

        override fun onReceive(context: Context, intent: Intent) {
            when (intent.action) {
                WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION -> onConnectionChanged?.invoke()
            }
        }
    }
}
