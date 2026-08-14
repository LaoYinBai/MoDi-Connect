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
using NAudio.Wave;
using MoDi.Core.Infrastructure;
using MoDi.Desktop;

namespace MoDi.Core.Adapters;

/// <summary>
/// SpeakerRenderer — 扬声器渲染适配器
///
/// 包裹 WaveOutEvent + BufferedWaveProvider，实现 IAudioRenderer。
/// 输出目标：系统默认扬声器设备。
/// </summary>
public sealed class SpeakerRenderer : IAudioRenderer, IDisposable
{
    private const string Tag = "SpeakerRenderer";

    private IWavePlayer? _output;
    private BufferedWaveProvider? _buffer;
    private WaveFormat? _waveFormat;
    private float _volume = 1.0f;
    private bool _muted;
    private bool _disposed;

    public bool IsReady => _output != null;

    public bool Prepare(AudioConfig config)
    {
        if (_output != null) return true;

        try
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(config.SampleRate, 1);
            _buffer = new BufferedWaveProvider(_waveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(config.BufferMs),
                DiscardOnBufferOverflow = true,
            };

            _output = new WaveOutEvent { DesiredLatency = config.WaveOutLatency };
            _output.Init(_buffer);
            return true;
        }
        catch (Exception ex)
        {
            Log.E(Tag, $"Prepare failed: {ex.Message}");
            Release();
            return false;
        }
    }

    public void Play()
    {
        _output?.Play();
    }

    public void Stop()
    {
        _output?.Stop();
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        if (_output != null)
            _output.Volume = _muted ? 0f : _volume;
    }

    public void Mute(bool muted)
    {
        _muted = muted;
        if (_output != null)
            _output.Volume = muted ? 0f : _volume;
    }

    public void FeedPcm(byte[] data, int offset, int count)
    {
        _buffer?.AddSamples(data, offset, count);
    }

    public void Release()
    {
        if (_disposed) return;
        _disposed = true;

        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _buffer = null;
    }

    public void Dispose() => Release();
}
