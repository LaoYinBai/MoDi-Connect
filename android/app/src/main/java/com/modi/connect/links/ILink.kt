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
package com.modi.connect.links

import android.media.projection.MediaProjection
import com.modi.connect.session.DisconnectReason
import java.util.UUID

/**
 * 链路状态（双端统一）
 */
enum class LinkState {
    IDLE,        // 未启动
    LISTENING,   // 监听中（等待连接）
    CONNECTED,   // 已连接（握手成功）
    STREAMING,   // 音频传输中
}

/**
 * ILink — 统一链路接口（双端对齐）
 *
 * 所有链路（WiFi LAN / WiFi Direct / Bluetooth / USB）实现此接口。
 * 发送端语义：connect = 主动发起连接 + 推流。
 * LinkManager 通过 when(linkType) 分发，不关心具体实现。
 */
interface ILink {
    /** 当前握手成功的会话标识；未连接时为 null */
    val sessionId: UUID?

    /** 链路当前状态 */
    val state: LinkState

    /** 链路是否正在推流 */
    val isStreaming: Boolean

    /** 状态回调（LinkManager 统一订阅） */
    var onStatusChanged: ((String) -> Unit)?
    var onStreamingChanged: ((Boolean) -> Unit)?

    /** 连接并推流 */
    suspend fun connect(params: LinkParams): Boolean

    /** 推流中热切路由 */
    suspend fun sendRouteUpdate(route: Int, proj: MediaProjection?): Boolean

    /** 通过仍存活的原链路发送应用层断开请求，不等待 ACK */
    suspend fun sendDisconnectRequest(targetLink: Byte, reason: DisconnectReason): Boolean

    /** 断开 */
    suspend fun disconnect()
}

/**
 * 连接参数（统一入口，各链路取自己需要的字段）
 */
data class LinkParams(
    val host: String? = null,          // LAN: 目标 IP
    val token: String? = null,         // P2P: QR 码 token
    val deviceName: String? = null,    // P2P: 对端设备名（配对持久化用）
    val route: Int = 0,                // 路线 0-3
    val proj: MediaProjection? = null  // 系统音频授权
)
