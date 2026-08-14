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
using System;
using MoDi.Desktop;

namespace MoDi.Core.Adapters;

/// <summary>
/// StubCapturer — 采集器桩实现（Windows 端不采集音频）
///
/// Windows 是纯接收端，不需要采集功能。
/// 所有方法抛出 NotSupportedException。
/// 以后 iOS/macOS 加入时各自实现真正的采集器。
/// </summary>
public sealed class StubCapturer : IAudioCapturer
{
    public CapturerType SourceType => CapturerType.Microphone;

    public bool Prepare(AudioConfig config)
        => throw new NotSupportedException("Windows 端不支持音频采集");

    public bool Start()
        => throw new NotSupportedException("Windows 端不支持音频采集");

    public int ReadFrame(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("Windows 端不支持音频采集");

    public void Stop() { }

    public void Release() { }
}
