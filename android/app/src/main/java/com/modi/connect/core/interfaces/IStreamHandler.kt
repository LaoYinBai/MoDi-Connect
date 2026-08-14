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
 * IStreamHandler — 流式数据处理器接口
 *
 * 处理通过 IDataChannel 传输的流式数据。
 * 音频管线（AudioPipeline）在 P5 中改为此接口的实现，
 * 注册到 IDataChannel 后由通道统一调度。
 *
 * 设计原则：
 *   - 处理器只关心"数据处理"，不关心底层传输
 *   - 通过 StreamType 标识处理哪种流
 *   - 未来视频流、文件流各自实现此接口，插件式注册
 *   - 预留双向扩展：onDataReceived 当前仅单向场景不触发
 */
interface IStreamHandler {
    /** 处理器标识（用于日志和调试） */
    val handlerId: String

    /** 处理的流类型 */
    val streamType: StreamType

    /** 处理器是否已激活 */
    val isActive: Boolean

    /**
     * 处理一帧输出数据（单向：本端产生 → 通道发送）。
     * 例：AudioPipeline 编码后的 Opus 帧通过此方法交给通道发送。
     */
    fun onDataToSend(data: ByteArray)

    /**
     * 处理一帧接收数据（预留双向扩展点）。
     * 当前单向发送模式下不调用。
     */
    fun onDataReceived(data: ByteArray)

    /** 流开始（通道打开后调用） */
    fun onStreamStart()

    /** 流结束（通道关闭前调用） */
    fun onStreamStop()

    /** 流错误通知 */
    fun onError(message: String, ex: Exception? = null)
}
