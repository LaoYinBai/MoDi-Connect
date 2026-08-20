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
package com.modi.connect.audio

import io.github.jaredmdobson.OpusEncoder
import io.github.jaredmdobson.OpusApplication
import io.github.jaredmdobson.OpusSignal
import java.nio.ByteBuffer
import java.nio.ByteOrder
import com.modi.connect.core.infrastructure.Log

/**
 * Opus 编码器（Concentus 纯 Java 实现）
 *
 * 输入：48kHz / 单声道 / 16bit PCM（1920 字节/帧，20ms）
 * 输出：Opus 包（可变长度，~320 字节 @ 128kbps）
 *
 * 线程安全约定：上层 [AudioPipeline] 保证单线程串行调用
 */
class AudioEncoder {

    companion object {
        private const val TAG = "AudioEncoder"
        const val SAMPLE_RATE = 48000
        const val FRAME_MS = 20
        val FRAME_SIZE = SAMPLE_RATE * FRAME_MS / 1000  // 960 采样点
        val FRAME_BYTES = FRAME_SIZE * 2                  // 1920 字节
        const val BITRATE = 128000                         // 128 kbps（局域网带宽充裕，最大化质量）
    }

    private var enc: OpusEncoder? = null
    private val _config: AudioConfig

    /** 构造 AudioEncoder，接收 AudioConfig 参数 */
    constructor(config: AudioConfig = AudioConfig.DEFAULT) {
        _config = config
    }

    /** 初始化 Opus 编码器。返回 false 表示创建失败。 */
    fun prepare(): Boolean = try {
        enc = OpusEncoder(SAMPLE_RATE, 1, OpusApplication.OPUS_APPLICATION_AUDIO).apply {
            setBitrate(_config.bitrate)
            setComplexity(_config.complexity)
            setUseInbandFEC(_config.useFec)
            setPacketLossPercent(_config.packetLossPercent)
            setUseConstrainedVBR(_config.useConstrainedVbr)
            setSignalType(OpusSignal.OPUS_SIGNAL_MUSIC)
        }
        // 预热：编码一帧静音，提前触发 Opus JNI 的 JIT 编译（否则首帧会阻塞 2+ 秒）
        encodeFrame(ByteArray(FRAME_BYTES))
        true
    } catch (e: Exception) {
        Log.e(TAG, "AUDIO_ENCODER_START_FAILED: ${e.message}")
        false
    }

    /** 编码一帧 PCM 数据（精确 1920 字节 PCM16LE），返回 Opus 包或 null */
    fun encodeFrame(pcm: ByteArray): ByteArray? {
        val e = enc ?: return null
        if (pcm.size != FRAME_BYTES) return null
        return try {
            // byte[] → short[] → Opus 编码
            val s = ShortArray(FRAME_SIZE)
            ByteBuffer.wrap(pcm).order(ByteOrder.LITTLE_ENDIAN).asShortBuffer().get(s)
            val out = ByteArray(4000)  // Opus 最大包大小
            val n = e.encode(s, 0, FRAME_SIZE, out, 0, out.size)
            if (n > 0) out.copyOf(n) else null
        } catch (e: Exception) {
            Log.w(TAG, "AUDIO_ENCODE_FAILED: ${e.message}")
            null
        }
    }

    fun release() { enc = null }
}
