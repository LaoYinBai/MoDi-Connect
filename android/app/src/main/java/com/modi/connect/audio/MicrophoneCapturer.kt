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

import android.media.AudioFormat
import android.media.AudioRecord
import android.media.MediaRecorder
import java.util.concurrent.atomic.AtomicBoolean
import com.modi.connect.core.infrastructure.Log
import kotlin.math.roundToInt

/**
 * 麦克风采集器
 * 48kHz / 16bit / 单声道 / 20ms 帧
 */
class MicrophoneCapturer {

    companion object {
        private const val TAG = "MicrophoneCapturer"
        const val SAMPLE_RATE = 48000
        const val FRAME_MS = 20
        val FRAME_SIZE = SAMPLE_RATE * FRAME_MS / 1000  // 960
        val FRAME_BYTES = FRAME_SIZE * 2                 // 1920
    }

    private var rec: AudioRecord? = null
    private val _config: AudioConfig

    /** 构造 MicrophoneCapturer，接收 AudioConfig 参数 */
    constructor(config: AudioConfig = AudioConfig.DEFAULT) {
        _config = config
    }
    private val running = AtomicBoolean(false)

    /** 输出音量 (0.0 ~ 1.0)，在 readFrame 中应用 */
    var volume: Float = 1.0f

    // ── 200Hz 高通滤波器（去风扇/机箱低频底噪，2026-07-09 新增） ──
    // 替代原 Windows 端 7kHz LPF 方案（已注释），只切低频保留高频细节
    private val hpf = Hpf()

    /**
     * 二阶 Butterworth 高通 IIR，200Hz @ 48kHz
     * 系数预计算：fc=200Hz, fs=48kHz, Q=0.7071
     */
    private class Hpf {
        companion object {
            private const val B0 = 0.98166f
            private const val B1 = -1.96333f
            private const val B2 = 0.98166f
            private const val A1 = -1.96298f
            private const val A2 = 0.96367f
        }

        private var x1 = 0f; private var x2 = 0f  // 输入历史
        private var y1 = 0f; private var y2 = 0f  // 输出历史

        fun process(pcmBytes: ByteArray) {
            val buf = java.nio.ByteBuffer.wrap(pcmBytes).order(java.nio.ByteOrder.LITTLE_ENDIAN)
            val n = pcmBytes.size / 2
            for (i in 0 until n) {
                val x = buf.getShort(i * 2).toFloat()
                val y = B0 * x + B1 * x1 + B2 * x2 - A1 * y1 - A2 * y2
                x2 = x1; x1 = x
                y2 = y1; y1 = y
                buf.putShort(i * 2, y.roundToInt().coerceIn(-32768, 32767).toShort())
            }
        }

        fun reset() { x1 = 0f; x2 = 0f; y1 = 0f; y2 = 0f }
    }

    fun prepare(): Boolean {
        if (rec != null) return true
        val buf = maxOf(
            AudioRecord.getMinBufferSize(SAMPLE_RATE, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT),
            FRAME_BYTES * _config.micBufferMultiplier
        )
        rec = try {
            AudioRecord(
                MediaRecorder.AudioSource.MIC, SAMPLE_RATE,
                AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT, buf
            )
        } catch (e: Exception) {
            Log.e(TAG, "AUDIO_CAPTURE_START_FAILED: ${e.message}")
            return false
        }
        return rec?.state == AudioRecord.STATE_INITIALIZED
    }

    fun start() {
        rec?.let { if (!running.get()) { it.startRecording(); running.set(true); hpf.reset() } }
    }

    /** HAL 预热：丢弃前几帧，确保后续 read 稳定 20ms 返回（在采集线程中调用） */
    fun warmup() {
        val r = rec ?: return
        if (!running.get()) return
        val buf = ByteArray(FRAME_BYTES)
        val deadline = System.currentTimeMillis() + 500
        var frames = 0
        while (frames < 3 && System.currentTimeMillis() < deadline) {
            if (r.read(buf, 0, FRAME_BYTES) > 0) frames++
        }
    }

    fun readFrame(): ByteArray? {
        val r = rec ?: return null
        if (!running.get()) return null
        val buf = ByteArray(FRAME_BYTES)
        return when (val n = r.read(buf, 0, FRAME_BYTES)) {
            FRAME_BYTES -> {
                hpf.process(buf)   // 200Hz 高通去低频底噪
                applyVolume(buf)   // 音量缩放
            }
            in 1 until FRAME_BYTES -> {
                val trimmed = buf.copyOf(n)
                hpf.process(trimmed)
                applyVolume(trimmed)
            }
            else -> null
        }
    }

    /** 应用音量缩放（volume=1.0 时跳过以节省 CPU） */
    private fun applyVolume(pcm: ByteArray): ByteArray {
        if (volume >= 0.999f) return pcm
        val s = ShortArray(pcm.size / 2)
        java.nio.ByteBuffer.wrap(pcm).order(java.nio.ByteOrder.LITTLE_ENDIAN).asShortBuffer().get(s)
        for (i in s.indices) {
            s[i] = (s[i] * volume).toInt().coerceIn(-32768, 32767).toShort()
        }
        val out = ByteArray(pcm.size)
        java.nio.ByteBuffer.wrap(out).order(java.nio.ByteOrder.LITTLE_ENDIAN).asShortBuffer().put(s)
        return out
    }

    /** 看门狗触发后重建 AudioRecord（stop → release → prepare → start） */
    fun restart(): Boolean {
        stop()
        rec?.release()
        rec = null
        hpf.reset()
        if (!prepare()) return false
        start()
        return true
    }

    fun stop() {
        running.set(false)
        try { rec?.stop() } catch (e: Exception) {
            Log.w(TAG, "AUDIO_CAPTURE_RELEASE_FAILED: ${e.message}")
        }
    }

    fun release() {
        stop()
        rec?.release()
        rec = null
    }
}
