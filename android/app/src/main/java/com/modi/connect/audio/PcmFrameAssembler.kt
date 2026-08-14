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
 * PCM 拼帧器 — 将任意长度 PCM 数据拼成固定帧长
 *
 * 采集器返回的数据可能是任意长度（取决于 AudioRecord 内部缓冲），
 * 拼帧器将其累积拼成固定 1920 字节（960 采样点 × 2 字节）帧后回调。
 */
class PcmFrameAssembler(private val frameBytes: Int) {
    private val pending = ByteArray(frameBytes)
    private var pendingLen = 0

    /** 喂入数据，每凑满一帧触发 onFrame 回调 */
    fun push(data: ByteArray, onFrame: (ByteArray) -> Unit) {
        var offset = 0
        while (offset < data.size) {
            val n = minOf(frameBytes - pendingLen, data.size - offset)
            System.arraycopy(data, offset, pending, pendingLen, n)
            pendingLen += n
            offset += n
            if (pendingLen == frameBytes) {
                onFrame(pending.copyOf())
                pendingLen = 0
            }
        }
    }

    /** 重置拼帧状态（新会话/切模式时调用） */
    fun reset() { pendingLen = 0 }
}
