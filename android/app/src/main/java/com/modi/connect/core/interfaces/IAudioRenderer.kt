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

/**
 * IAudioRenderer — 统一音频渲染接口
 *
 * 提供 PCM 到扬声器/虚拟麦克风的输出。
 * 不包含路由逻辑（路由是业务层的职责）。
 *
 * 当前实现：
 *   Windows — SpeakerRenderer / CableRenderer
 *   Android — StubRenderer（Android 是纯发送端，不渲染）
 */
interface IAudioRenderer {
    /** 准备渲染器（分配缓冲和设备） */
    fun prepare(config: AudioConfig): Boolean

    /** 开始播放 */
    fun play()

    /** 停止播放 */
    fun stop()

    /** 设置音量（0.0 ~ 1.0） */
    fun setVolume(volume: Float)

    /** 静音/取消静音 */
    fun mute(muted: Boolean)

    /** 喂入一帧 PCM 数据（IEEE Float32 little-endian） */
    fun feedPcm(data: ByteArray, offset: Int, count: Int)

    /** 释放所有资源 */
    fun release()

    /** 是否已就绪（设备已打开） */
    val isReady: Boolean
}
