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

import com.modi.connect.audio.AudioConfig
import com.modi.connect.core.interfaces.IAudioRenderer

/**
 * StubRenderer — 渲染器桩实现（Android 端不渲染音频）
 *
 * Android 是纯发送端，不需要渲染功能。
 * 所有方法为空操作或抛出 NotSupportedException。
 */
class StubRenderer : IAudioRenderer {

    override val isReady: Boolean = false

    override fun prepare(config: AudioConfig): Boolean = false

    override fun play() {}

    override fun stop() {}

    override fun setVolume(volume: Float) {}

    override fun mute(muted: Boolean) {}

    override fun feedPcm(data: ByteArray, offset: Int, count: Int) {}

    override fun release() {}
}
