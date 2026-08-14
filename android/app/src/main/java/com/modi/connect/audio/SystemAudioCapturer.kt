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

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioManager
import android.media.AudioPlaybackCaptureConfiguration
import android.media.AudioRecord
import android.media.projection.MediaProjection
import android.os.Build
import android.os.Handler
import android.os.Looper
import com.modi.connect.core.infrastructure.Log
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.atomic.AtomicBoolean

/**
 * 系统音频采集器（MediaProjection）
 * 采集手机内部音频（媒体/游戏），支持音量调整和强制静音锁定
 *
 * Android 10+ 要求：
 * - 必须先启动 [MediaProjectionService] 获取授权
 * - 推流中需后台运行 [StreamingService] 维持采集
 */
class SystemAudioCapturer {

    companion object {
        private const val TAG = "SystemAudioCapturer"
        const val SAMPLE_RATE = 48000
        const val FRAME_MS = 20
        val FRAME_SIZE = SAMPLE_RATE * FRAME_MS / 1000  // 960 采样点
        val FRAME_BYTES = FRAME_SIZE * 2                 // 1920 字节
        private const val POLL_MS = 100L
    }

    private var rec: AudioRecord? = null
    private val _config: AudioConfig
    private val running = AtomicBoolean(false)
    private var audioManager: AudioManager? = null
    private var cachedProjection: MediaProjection? = null  // 看门狗 restart 用
    private var cachedCtx: Context? = null

    constructor(config: AudioConfig = AudioConfig.DEFAULT) {
        _config = config
    }
    private var originalVolume = -1
    private var pollHandler: Handler? = null
    private var pollTask: Runnable? = null

    /** 输出音量 (0.0 ~ 1.0)，在 readFrame 中应用 */
    var volume: Float = 1.0f

    /**
     * 初始化系统音频采集器
     * @param projection 用户授权的 MediaProjection 实例
     */
    fun prepare(projection: MediaProjection, ctx: Context? = null): Boolean {
        if (rec != null) return true
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return false
        cachedProjection = projection
        cachedCtx = ctx

        audioManager = ctx?.getSystemService(Context.AUDIO_SERVICE) as? AudioManager

        // ── 强制静音：采集期间媒体音量静音，防止回环啸叫 ──
        audioManager?.let { mgr ->
            originalVolume = mgr.getStreamVolume(AudioManager.STREAM_MUSIC)
            mgr.setStreamVolume(AudioManager.STREAM_MUSIC, 0, 0)
            Log.i(TAG, "muted, was $originalVolume")
            startSilenceLock(mgr)  // 轮询确保不被用户调高
        }

        try {
            val config = AudioPlaybackCaptureConfiguration.Builder(projection)
                .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
                .addMatchingUsage(AudioAttributes.USAGE_GAME)
                .build()
            val fmt = AudioFormat.Builder()
                .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                .setSampleRate(SAMPLE_RATE)
                .setChannelMask(AudioFormat.CHANNEL_IN_MONO)
                .build()
            // 大缓冲区 61440 bytes ≈ 640ms，吸收系统音频捕获的初始抖动
            val buf = maxOf(
                AudioRecord.getMinBufferSize(SAMPLE_RATE, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT),
                FRAME_BYTES * _config.sysAudioBufferMultiplier)
            rec = AudioRecord.Builder()
                .setAudioPlaybackCaptureConfig(config).setAudioFormat(fmt).setBufferSizeInBytes(buf).build()
            Log.i(TAG, "init ok")
            return rec?.state == AudioRecord.STATE_INITIALIZED
        } catch (e: Exception) {
            Log.e(TAG, "init: ${e.message}"); return false
        }
    }

    // ── 强制静音轮询：每 100ms 检查一次，确保音量归零 ──
    private fun startSilenceLock(mgr: AudioManager) {
        pollHandler = Handler(Looper.getMainLooper())
        pollTask = Runnable {
            try {
                val cur = mgr.getStreamVolume(AudioManager.STREAM_MUSIC)
                if (cur != 0) mgr.setStreamVolume(AudioManager.STREAM_MUSIC, 0, 0)
            } catch (_: Exception) {}
            pollHandler?.postDelayed(pollTask!!, POLL_MS)
        }
        pollHandler?.postDelayed(pollTask!!, POLL_MS)
    }

    fun start() { rec?.let { if (!running.get()) { it.startRecording(); running.set(true) } } }

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

    /** 读取一帧 PCM 数据，带音量缩放 */
    fun readFrame(): ByteArray? {
        val r = rec ?: return null
        if (!running.get()) return null
        val buf = ByteArray(FRAME_BYTES)
        return when (val n = r.read(buf, 0, FRAME_BYTES)) {
            FRAME_BYTES -> applyVolume(buf)        // 完整一帧
            in 1 until FRAME_BYTES -> applyVolume(buf.copyOf(n))  // 不完整帧
            else -> null
        }
    }

    /** 应用音量缩放（volume=1.0 时跳过以节省 CPU） */
    private fun applyVolume(pcm: ByteArray): ByteArray {
        if (volume >= 0.999f) return pcm
        val s = ShortArray(pcm.size / 2)
        ByteBuffer.wrap(pcm).order(ByteOrder.LITTLE_ENDIAN).asShortBuffer().get(s)
        for (i in s.indices) {
            s[i] = (s[i] * volume).toInt().coerceIn(-32768, 32767).toShort()
        }
        val out = ByteArray(pcm.size)
        ByteBuffer.wrap(out).order(ByteOrder.LITTLE_ENDIAN).asShortBuffer().put(s)
        return out
    }

    /** 看门狗触发后重建 AudioRecord（保留静音轮询，不重新设置音量） */
    fun restart(): Boolean {
        val proj = cachedProjection ?: return false
        stop()
        rec?.release()
        rec = null
        // 重新构建 AudioRecord（复用已缓存的 projection，不重复静音）
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return false
        try {
            val config = AudioPlaybackCaptureConfiguration.Builder(proj)
                .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
                .addMatchingUsage(AudioAttributes.USAGE_GAME)
                .build()
            val fmt = AudioFormat.Builder()
                .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                .setSampleRate(SAMPLE_RATE)
                .setChannelMask(AudioFormat.CHANNEL_IN_MONO)
                .build()
            val buf = maxOf(
                AudioRecord.getMinBufferSize(SAMPLE_RATE, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT),
                FRAME_BYTES * _config.sysAudioBufferMultiplier)
            rec = AudioRecord.Builder()
                .setAudioPlaybackCaptureConfig(config).setAudioFormat(fmt).setBufferSizeInBytes(buf).build()
            if (rec?.state != AudioRecord.STATE_INITIALIZED) return false
            start()
            Log.i(TAG, "restart ok")
            return true
        } catch (e: Exception) {
            Log.e(TAG, "restart failed: ${e.message}")
            return false
        }
    }

    fun stop() {
        running.set(false)
        try { rec?.stop() } catch (_: Exception) {}
    }

    /** 释放资源并恢复系统音量 */
    fun release() {
        stop()
        pollHandler?.removeCallbacks(pollTask!!)  // 停止静音轮询
        pollHandler = null
        pollTask = null
        rec?.release()
        rec = null
        // 恢复采集前的媒体音量
        if (originalVolume >= 0) {
            audioManager?.setStreamVolume(AudioManager.STREAM_MUSIC, originalVolume, 0)
            originalVolume = -1
        }
    }
}
