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
import com.modi.connect.core.enums.CapturerType
import com.modi.protocol.TransportType
import com.modi.connect.core.impl.LogcatLogger
import com.modi.connect.core.interfaces.*

/**
 * PlatformFactory — 平台工厂
 *
 * 第一批（P3 已实现）：createLogger / createTransport / createProtocol
 * 第二批（P3 新增）：createCapturer / createRenderer / createDiscovery / createNetworkMonitor
 */
object PlatformFactory {

    // ── 第一批工厂方法（P3 已实现） ──

    fun createLogger(): ILogger = LogcatLogger()

    /**
     * 创建传输层实例。
     * 当前所有链路共用 UDP 传输，后续链路分离后由各链路文件提供专属工厂方法。
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

    // ── 第二批工厂方法（P3 新增） ──

    /**
     * 创建音频采集器。
     * 注意：SystemAudio 类型需要 MediaProjection，请使用 SystemAudioCapturerAdapter 构造函数。
     */
    fun createCapturer(type: CapturerType = CapturerType.Microphone): IAudioCapturer {
        return when (type) {
            CapturerType.Microphone -> MicCapturerAdapter()
            // SystemAudio 和 Mixed 需要 MediaProjection，由应用层直接构造 Adapter
            else -> MicCapturerAdapter()
        }
    }

    /**
     * 创建音频渲染器。
     * Android 端为桩实现（Android 是纯发送端，不渲染音频）。
     */
    fun createRenderer(): IAudioRenderer = StubRenderer()

    /** 创建设备发现实例（NsdManager 扫描 _modi._udp） */
    fun createDiscovery(context: Context): IDiscovery = NsdDiscoveryAdapter(context)

    /** 创建网络状态监听实例 */
    fun createNetworkMonitor(context: Context): INetworkMonitor = AndroidNetworkMonitor(context)
}
