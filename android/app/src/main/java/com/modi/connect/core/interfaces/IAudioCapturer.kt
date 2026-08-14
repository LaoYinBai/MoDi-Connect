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

import com.modi.connect.audio.AudioConfig
import com.modi.connect.core.enums.CapturerType

/**
 * IAudioCapturer — 统一音频采集接口（纯 PCM 层）
 *
 * 采集原始 PCM16LE 帧，不涉及 Opus 编解码。
 * 编解码由应用层 Pipeline 处理。
 *
 * 当前实现：
 *   Android — MicCapturerAdapter / SystemAudioCapturerAdapter
 *   Windows — StubCapturer（Windows 是纯接收端，不采集）
 */
interface IAudioCapturer {
    /** 准备采集器（分配资源） */
    fun prepare(config: AudioConfig): Boolean

    /** 开始采集 */
    fun start(): Boolean

    /** 读取一帧 PCM16LE，返回实际读取的字节数（0 表示无数据） */
    fun readFrame(buffer: ByteArray, offset: Int, count: Int): Int

    /** 停止采集 */
    fun stop()

    /** 释放所有资源 */
    fun release()

    /** HAL 预热（丢弃前几帧，消除冷启动抖动） */
    fun warmup() {}

    /** 看门狗触发时重建采集器（默认返回 false 表示不支持） */
    fun restart(): Boolean = false

    /** 采集源类型 */
    val sourceType: CapturerType
}
