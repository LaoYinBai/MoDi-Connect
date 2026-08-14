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
using MoDi.Desktop;

namespace MoDi.Core;

/// <summary>
/// IAudioRenderer — 统一音频渲染接口
///
/// 提供 PCM 到扬声器/虚拟麦克风的输出。
/// 不包含路由逻辑（路由是业务层 AudioRouter 的职责）。
///
/// 当前实现：
///   Windows — SpeakerRenderer（WaveOutEvent 扬声器）/ CableRenderer（WasapiOut CABLE Input）
///   Android — StubRenderer（Android 是纯发送端，不渲染）
/// </summary>
public interface IAudioRenderer
{
    /// <summary>准备渲染器（分配缓冲和设备）</summary>
    bool Prepare(AudioConfig config);

    /// <summary>开始播放</summary>
    void Play();

    /// <summary>停止播放</summary>
    void Stop();

    /// <summary>设置音量（0.0 ~ 1.0）</summary>
    void SetVolume(float volume);

    /// <summary>静音/取消静音</summary>
    void Mute(bool muted);

    /// <summary>喂入一帧 PCM 数据（IEEE Float32 little-endian）</summary>
    void FeedPcm(byte[] data, int offset, int count);

    /// <summary>释放所有资源</summary>
    void Release();

    /// <summary>是否已就绪（设备已打开）</summary>
    bool IsReady { get; }
}
