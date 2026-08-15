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
package com.modi.connect.core.factory

import com.modi.protocol.ITransport
import com.modi.protocol.IPacketProtocol
import com.modi.protocol.PacketHeaderCodec

import android.content.Context
import com.modi.connect.core.adapters.*
import com.modi.protocol.TransportType
import com.modi.connect.core.interfaces.*

/**
 * PlatformFactory — 平台工厂
 *
 * 只提供实际被链路/音频层消费的工厂方法：
 * createTransport / createProtocol / createDiscovery / createNetworkMonitor。
 * 日志走 Log.setImpl；采集器由 CaptureLoop 按模式直接构造（需要 MediaProjection）。
 */
object PlatformFactory {

    /**
     * 创建传输层实例。
     *
     * @param type 传输类型
     * @param host 远程主机（null = server 模式）
     * @param port 端口（server 模式为绑定端口，client 模式为远程端口）
     * @param localPort 本地绑定端口（0 = 随机，仅 client 模式）
     * @param localBindAddress 本地绑定地址（P2P 模式绑定到 P2P 接口 IP）
     */
    fun createTransport(
        type: TransportType,
        host: String? = null,
        port: Int = 12345,
        localPort: Int = 0,
        localBindAddress: String? = null
    ): ITransport {
        return when (type) {
            TransportType.Udp -> UdpTransport(
                localPort = if (host != null) localPort else port,
                remoteHost = host,
                remotePort = port,
                localBindAddress = localBindAddress
            )
            // Bluetooth 链路由 BluetoothLink 直接创建 BluetoothTransport（需要已连接的 BluetoothSocket）
            TransportType.Bluetooth -> throw IllegalStateException(
                "BluetoothTransport must be created by BluetoothLink (requires accepted BluetoothSocket)"
            )
            // USB 链路由 UsbLink 直接创建 UsbTransport（需要 TCP Server 监听）
            TransportType.Usb -> throw IllegalStateException(
                "UsbTransport must be created by UsbLink (requires TCP server listening)"
            )
        }
    }

    fun createProtocol(): IPacketProtocol = PacketHeaderCodec()

    /** 创建设备发现实例（NsdManager 扫描 _modi._udp） */
    fun createDiscovery(context: Context): IDiscovery = NsdDiscoveryAdapter(context)

    /** 创建网络状态监听实例 */
    fun createNetworkMonitor(context: Context): INetworkMonitor = AndroidNetworkMonitor(context)
}
