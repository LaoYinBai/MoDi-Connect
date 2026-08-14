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
package com.modi.connect.core.adapters

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.net.NetworkRequest
import com.modi.connect.core.enums.NetworkQuality
import com.modi.protocol.TransportType
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.core.interfaces.INetworkMonitor
import com.modi.connect.core.models.NetworkInfo

/**
 * AndroidNetworkMonitor — Android 网络状态监听适配器
 *
 * 基于 ConnectivityManager.NetworkCallback。
 * 从 ReconnectionManager 拆分出的纯网络监听逻辑。
 * 职责仅限状态监听，不含重连逻辑。
 */
class AndroidNetworkMonitor(
    private val context: Context
) : INetworkMonitor {

    companion object {
        private const val TAG = "AndroidNetworkMonitor"
    }

    private var cm: ConnectivityManager? = null
    private var started = false

    override var isConnected: Boolean = false
        private set
    override var activeTransport: TransportType = TransportType.Udp
        private set
    override var quality: NetworkQuality = NetworkQuality.Unknown
        private set
    override var onNetworkChanged: ((NetworkInfo) -> Unit)? = null

    private val networkCallback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) {
            Log.i(TAG, "Network available")
            updateState(connected = true, quality = NetworkQuality.Good)
        }

        override fun onLost(network: Network) {
            Log.i(TAG, "Network lost")
            updateState(connected = false, quality = NetworkQuality.Disconnected)
        }

        override fun onCapabilitiesChanged(network: Network, caps: NetworkCapabilities) {
            val hasInternet = caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
            val isWifi = caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)
            val isCellular = caps.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR)

            val q = when {
                !hasInternet -> NetworkQuality.Poor
                isWifi -> NetworkQuality.Good
                isCellular -> NetworkQuality.Poor
                else -> NetworkQuality.Unknown
            }
            updateState(connected = hasInternet, quality = q)
        }
    }

    override fun start() {
        if (started) return
        started = true

        cm = context.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        val request = NetworkRequest.Builder()
            .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
            .build()
        cm?.registerNetworkCallback(request, networkCallback)

        // 初始状态评估
        val activeNetwork = cm?.activeNetwork
        val caps = activeNetwork?.let { cm?.getNetworkCapabilities(it) }
        isConnected = caps?.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) ?: false
        quality = if (isConnected) NetworkQuality.Good else NetworkQuality.Disconnected

        Log.i(TAG, "Started: isConnected=$isConnected, quality=$quality")
    }

    override fun stop() {
        if (!started) return
        started = false

        try {
            cm?.unregisterNetworkCallback(networkCallback)
        } catch (_: Exception) {}
        Log.i(TAG, "Stopped")
    }

    private fun updateState(connected: Boolean, quality: NetworkQuality) {
        isConnected = connected
        this.quality = quality

        val info = NetworkInfo(
            isConnected = connected,
            transportType = activeTransport,
            quality = quality,
            ssid = null,
            interfaceName = null
        )
        onNetworkChanged?.invoke(info)
    }
}
