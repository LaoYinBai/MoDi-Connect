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
import android.media.projection.MediaProjection
import com.modi.connect.core.adapters.MicCapturerAdapter
import com.modi.connect.core.adapters.SystemAudioCapturerAdapter
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.core.interfaces.IAudioCapturer

/**
 * CaptureLoop — 采集循环 + 看门狗
 *
 * 职责：管理采集线程生命周期，单源/混音采集循环，看门狗重启。
 * 不关心编码和发送，只产出 PCM 帧通过回调交给 EncodeSender。
 *
 * 状态机：IDLE → RUNNING → STOPPING → IDLE
 * 禁止非法转换（如 RUNNING→RUNNING、IDLE→STOPPING）
 */
class CaptureLoop(
    private val config: AudioConfig,
    private val streamGain: StreamGain,
    private val onPcmFrame: (ByteArray) -> Unit
) {
    companion object {
        private const val TAG = "CaptureLoop"
        private const val WATCHDOG_NULL_THRESHOLD = 15   // 连续 15 次 null（≈300ms）触发重启
        private const val WATCHDOG_MAX_RESTARTS = 5      // 最多重启次数
    }

    /** 采集状态枚举 */
    private enum class CaptureState { IDLE, RUNNING, STOPPING }

    @Volatile private var state = CaptureState.IDLE

    /** 外部查询：是否正在采集 */
    val streaming: Boolean get() = state == CaptureState.RUNNING

    private var thread: Thread? = null
    private var micCapturer: IAudioCapturer? = null
    private var sysCapturer: IAudioCapturer? = null

    /** 获取当前持有的 capturer 引用（供音量控制） */
    val mic: IAudioCapturer? get() = micCapturer
    val sys: IAudioCapturer? get() = sysCapturer

    /**
     * 启动采集线程
     * @return true 表示成功启动
     */
    fun start(mode: Int, proj: MediaProjection?, ctx: Context?): Boolean {
        if (state != CaptureState.IDLE) {
            Log.w(TAG, "start() rejected: state=$state")
            return false
        }
        // 释放上一轮残留的采集器：switchMode 会 stop 旧采集器后经 start 重建，
        // 若只覆盖引用不 release，残留 AudioRecord 在 MIUI/HyperOS 上会占住
        // audio policy 注册位，导致新播放捕获器报 "could not register audio policy"，
        // 热切越快越容易触发（此前靠 GC 终结器兜底，属竞态）。
        micCapturer?.release()
        sysCapturer?.release()
        micCapturer = null
        sysCapturer = null
        state = CaptureState.RUNNING
        thread = when (mode) {
            AudioPipeline.MODE_MIC -> {
                val mic = MicCapturerAdapter()
                mic.volume = streamGain.value
                if (!mic.prepare(config)) { state = CaptureState.IDLE; return false }
                mic.start()
                micCapturer = mic
                Thread({ mic.warmup(); singleLoop(mic) }, "cap-mic")
            }
            AudioPipeline.MODE_SYSTEM -> {
                if (proj == null) { state = CaptureState.IDLE; return false }
                val sys = SystemAudioCapturerAdapter(proj, ctx)
                sys.volume = streamGain.value
                if (!sys.prepare(config)) { state = CaptureState.IDLE; return false }
                sys.start()
                sysCapturer = sys
                Thread({ sys.warmup(); singleLoop(sys) }, "cap-sys")
            }
            AudioPipeline.MODE_MIX -> {
                if (proj == null) { state = CaptureState.IDLE; return false }
                val sys = SystemAudioCapturerAdapter(proj, ctx)
                val mic = MicCapturerAdapter()
                sys.volume = streamGain.value
                mic.volume = streamGain.value
                if (!sys.prepare(config)) { state = CaptureState.IDLE; return false }
                if (!mic.prepare(config)) { sys.release(); state = CaptureState.IDLE; return false }
                mic.start()
                sys.start()
                micCapturer = mic
                sysCapturer = sys
                Thread({ mic.warmup(); sys.warmup(); mixLoop(mic, sys) }, "cap-mix")
            }
            else -> null
        }
        val t = thread ?: run { state = CaptureState.IDLE; return false }
        t.start()
        return true
    }

    /**
     * 停止采集线程（不释放全部资源，供 switchMode 切换模式时使用）
     * @param releaseSystemAudio 是否释放系统音频采集器
     */
    fun stop(releaseSystemAudio: Boolean = false) {
        if (state != CaptureState.RUNNING) return
        state = CaptureState.STOPPING
        thread?.join(500)
        thread = null
        state = CaptureState.IDLE
        micCapturer?.stop()
        if (releaseSystemAudio) { sysCapturer?.release(); sysCapturer = null }
        else sysCapturer?.stop()
    }

    /**
     * 完全释放所有采集器资源（stopStreaming 时调用）
     * 停止线程并释放 micCapturer 和 sysCapturer
     */
    fun release() {
        state = CaptureState.STOPPING
        thread?.join(500)
        thread = null
        state = CaptureState.IDLE
        micCapturer?.stop(); micCapturer?.release(); micCapturer = null
        sysCapturer?.stop(); sysCapturer?.release(); sysCapturer = null
    }

    // ── 单源采集循环（MIC / SYSTEM 共用） ──

    private fun singleLoop(capturer: IAudioCapturer) {
        var failCount = 0
        var watchdogCount = 0
        var restartAttempts = 0
        val buf = ByteArray(AudioPipeline.FRAME_BYTES)

        while (state == CaptureState.RUNNING) {
            val n = capturer.readFrame(buf, 0, AudioPipeline.FRAME_BYTES)
            if (n > 0) {
                failCount = 0
                watchdogCount = 0
                restartAttempts = 0
                val pcm = if (n == AudioPipeline.FRAME_BYTES) buf else buf.copyOf(n)
                onPcmFrame(pcm)
            } else {
                failCount++
                watchdogCount++
                if (failCount > 10) { Thread.sleep(2); failCount = 0 }
                if (watchdogCount >= WATCHDOG_NULL_THRESHOLD && restartAttempts < WATCHDOG_MAX_RESTARTS) {
                    restartAttempts++
                    Log.w(TAG, "Watchdog: restarting capturer (attempt $restartAttempts)")
                    if (capturer.restart()) {
                        applyCurrentGain(capturer)
                        watchdogCount = 0
                    }
                    else Thread.sleep(50)
                }
            }
        }
        Log.i(TAG, "singleLoop exited")
    }

    // ── 混音采集循环（MODE_MIX） ──

    private fun mixLoop(mic: IAudioCapturer, sys: IAudioCapturer) {
        var failCount = 0
        var watchdogCount = 0
        var restartAttempts = 0
        val bufA = ByteArray(AudioPipeline.FRAME_BYTES)
        val bufB = ByteArray(AudioPipeline.FRAME_BYTES)

        while (state == CaptureState.RUNNING) {
            val nA = mic.readFrame(bufA, 0, AudioPipeline.FRAME_BYTES)
            val nB = sys.readFrame(bufB, 0, AudioPipeline.FRAME_BYTES)
            val pcmPart = when {
                nA > 0 && nB > 0 -> AudioMixer.mix(
                    if (nA == AudioPipeline.FRAME_BYTES) bufA else bufA.copyOf(nA),
                    if (nB == AudioPipeline.FRAME_BYTES) bufB else bufB.copyOf(nB)
                )
                nA > 0 -> if (nA == AudioPipeline.FRAME_BYTES) bufA else bufA.copyOf(nA)
                nB > 0 -> if (nB == AudioPipeline.FRAME_BYTES) bufB else bufB.copyOf(nB)
                else -> {
                    failCount++
                    watchdogCount++
                    if (failCount > 10) { Thread.sleep(2); failCount = 0 }
                    if (watchdogCount >= WATCHDOG_NULL_THRESHOLD && restartAttempts < WATCHDOG_MAX_RESTARTS) {
                        restartAttempts++
                        Log.w(TAG, "Mix watchdog: restarting capturers (attempt $restartAttempts)")
                        if (mic.restart()) applyCurrentGain(mic)
                        if (sys.restart()) applyCurrentGain(sys)
                        watchdogCount = 0
                    }
                    continue
                }
            }
            failCount = 0
            watchdogCount = 0
            restartAttempts = 0
            onPcmFrame(pcmPart)
        }
        Log.i(TAG, "mixLoop exited")
    }

    private fun applyCurrentGain(capturer: IAudioCapturer) {
        when (capturer) {
            is MicCapturerAdapter -> capturer.volume = streamGain.value
            is SystemAudioCapturerAdapter -> capturer.volume = streamGain.value
        }
    }
}
