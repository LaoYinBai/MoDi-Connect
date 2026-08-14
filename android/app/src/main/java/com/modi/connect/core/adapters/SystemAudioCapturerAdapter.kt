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
import android.media.projection.MediaProjection
import com.modi.connect.audio.AudioConfig
import com.modi.connect.audio.SystemAudioCapturer
import com.modi.connect.core.enums.CapturerType
import com.modi.connect.core.interfaces.IAudioCapturer

/**
 * SystemAudioCapturerAdapter — 系统音频采集适配器
 *
 * 包裹 [SystemAudioCapturer]，实现 [IAudioCapturer]。
 * MediaProjection 通过构造函数传入（Android 特有类型，不污染接口定义）。
 */
class SystemAudioCapturerAdapter(
    private val projection: MediaProjection,
    private val context: Context? = null
) : IAudioCapturer {

    private var capturer: SystemAudioCapturer? = null

    override val sourceType: CapturerType = CapturerType.SystemAudio

    override fun prepare(config: AudioConfig): Boolean {
        if (capturer != null) return true
        capturer = SystemAudioCapturer(config)
        return capturer?.prepare(projection, context) ?: false
    }

    override fun start(): Boolean {
        capturer?.start()
        return capturer != null
    }

    override fun readFrame(buffer: ByteArray, offset: Int, count: Int): Int {
        val pcm = capturer?.readFrame() ?: return 0
        val n = minOf(pcm.size, count)
        System.arraycopy(pcm, 0, buffer, offset, n)
        return n
    }

    override fun stop() {
        capturer?.stop()
    }

    override fun release() {
        capturer?.release()
        capturer = null
    }

    // ── 透传方法（供应用层使用，不属于接口） ──

    /** HAL 预热（丢弃前几帧） */
    override fun warmup() {
        capturer?.warmup()
    }

    /** 看门狗触发时重建 AudioRecord */
    override fun restart(): Boolean {
        return capturer?.restart() ?: false
    }

    /** 输出音量 */
    var volume: Float
        get() = capturer?.volume ?: 1.0f
        set(value) { capturer?.volume = value }
}
