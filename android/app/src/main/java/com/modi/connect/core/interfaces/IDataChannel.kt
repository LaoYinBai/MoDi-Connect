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
package com.modi.connect.core.interfaces

import com.modi.connect.core.enums.StreamType

/**
 * IDataChannel — 通用数据通道接口
 *
 * 抽象底层传输能力，可承载不同类型的流式数据。
 * 未来投屏、文件传输、剪切板同步均复用此接口，仅需注册不同的 IStreamHandler。
 *
 * 设计原则：
 *   - 通道只负责"搬运数据"，不关心数据内容
 *   - 通过 registerHandler 注册处理器实现插件化
 *   - 当前为单向通道（send），预留双向扩展点（receive）
 *   - 音频管线在 P5 中改为 IStreamHandler 实现注册到通道
 */
interface IDataChannel {
    /** 通道唯一标识 */
    val channelId: String

    /** 通道承载的流类型 */
    val streamType: StreamType

    /** 通道是否已打开 */
    val isOpen: Boolean

    /** 打开通道（建立底层传输连接） */
    suspend fun open()

    /** 关闭通道（释放底层传输连接） */
    suspend fun close()

    /** 发送数据（单向：本端 → 远端） */
    suspend fun send(data: ByteArray)

    /**
     * 接收数据（预留双向扩展点）。
     * 当前单向模式下抛出 UnsupportedOperationException。
     * 未来双向通道（如剪切板同步）实现此方法。
     */
    suspend fun receive(): ByteArray

    /** 注册流处理器（插件化：一个通道可挂载一个处理器） */
    fun registerHandler(handler: IStreamHandler)

    /** 卸载流处理器 */
    fun unregisterHandler()

    /** 通道状态变化时回调（true=已打开，false=已关闭） */
    var onStateChanged: ((Boolean) -> Unit)?
}
