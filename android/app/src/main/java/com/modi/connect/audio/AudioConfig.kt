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

/**
 * AudioConfig — Android 端音频参数集中配置
 *
 * ## 职责
 * 统一管理所有音频相关常量，提供默认参数。
 * 各模块通过构造函数接收此配置，消除分散的 hardcode。
 *
 * ## 默认值说明
 * - 采样率 48kHz / 16bit / 单声道 — 通用 AC 级语音/音乐传输
 * - 帧长 20ms（960 采样点）— Opus 推荐帧长，延迟与压缩率平衡
 * - Opus 码率 128kbps — 局域网带宽充足，音质优先
 * - 复杂度 10（最高）— 本机编码不计较 CPU
 * - FEC 开启 + 15% 预期丢包率 — 抗 WiFi 不稳定
 * - CVBR — 约束 VBR，为 FEC 留余量
 *
 * ## 缓冲说明
 * - MicrophoneCapturer: MinBufferMultiplier=4（~80ms），抗 AudioRecord 读取抖动
 * - SystemAudioCapturer: MinBufferMultiplier=32（~640ms），抗 MediaProjection 初始丢帧
 */
data class AudioConfig(
    /** 采样率（Hz），默认 48000 */
    val sampleRate: Int = 48000,
    /** 声道数，默认 1（单声道） */
    val channels: Int = 1,
    /** 帧长（毫秒），默认 20ms */
    val frameMs: Int = 20,
    /** Opus 编码码率（bps），默认 128000 */
    val bitrate: Int = 128000,
    /** Opus 编码复杂度（0-10），默认 10 */
    val complexity: Int = 10,
    /** 是否启用带内 FEC，默认 true */
    val useFec: Boolean = true,
    /** 预期丢包率百分比，默认 15 */
    val packetLossPercent: Int = 15,
    /** 是否使用约束 VBR，默认 true */
    val useConstrainedVbr: Boolean = true,
    /** Opus 信号类型，默认 OPUS_SIGNAL_MUSIC */
    val signalType: Int = 0,  // 0 = OPUS_SIGNAL_MUSIC
    /** 麦克风采集缓冲倍数，默认 4 */
    val micBufferMultiplier: Int = 4,
    /** 系统音频采集缓冲倍数，默认 32 */
    val sysAudioBufferMultiplier: Int = 32,
) {
    // ── 派生常量 ──

    /** 每帧采样点数 */
    val frameSize: Int get() = sampleRate * frameMs / 1000
    /** 每帧字节数（PCM16LE） */
    val frameBytes: Int get() = frameSize * 2  // 16bit = 2 字节

    companion object {
        /** 默认配置 */
        val DEFAULT = AudioConfig()
    }
}
