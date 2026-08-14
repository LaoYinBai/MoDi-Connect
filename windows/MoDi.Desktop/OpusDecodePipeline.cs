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
using Concentus.Structs;
using MoDi.Core.Infrastructure;
using MoDi.Protocol;

namespace MoDi.Desktop;

/// <summary>
/// OpusDecodePipeline — Opus 解码 + FEC + PLC + short→float 转换
///
/// 纯计算模块，无定时器，无状态管理。
/// 由 AudioEngine 调用，产出 float[] PCM 帧。
/// </summary>
public sealed class OpusDecodePipeline
{
    private readonly int _frameSize;
    private readonly object _decoderLock = new();  // 保护 _decoder 线程安全

#pragma warning disable CS0618
    private readonly OpusDecoder _decoder;
#pragma warning restore CS0618

    // ── FEC 序号追踪 ──
    private uint _lastSeq;
    private bool _hasLastSeq;
    private long _lostFrames;
    private long _fecRecovered;

    public long LostFrames => _lostFrames;
    public long FecRecovered => _fecRecovered;

    public OpusDecodePipeline(int sampleRate, int channels, int frameSize)
    {
        _frameSize = frameSize;
#pragma warning disable CS0618
        _decoder = new OpusDecoder(sampleRate, channels);
#pragma warning restore CS0618
    }

    /// <summary>正常解码：opus → float[] PCM（含音量缩放）</summary>
    public float[]? Decode(byte[] opus, float volume)
    {
        short[]? pcmShort;
        lock (_decoderLock) { pcmShort = DecodeShort(opus, false); }
        if (pcmShort == null) return null;
        return ShortToFloat(pcmShort, volume);
    }

    /// <summary>FEC 恢复：用当前包的冗余数据恢复丢失的前一帧</summary>
    public float[]? DecodeFec(byte[] currentOpus, float volume)
    {
        try
        {
            short[]? pcmShort;
            lock (_decoderLock) { pcmShort = DecodeShort(currentOpus, true); }
            if (pcmShort == null) return null;
            _fecRecovered++;
            return ShortToFloat(pcmShort, volume);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>PLC：用解码器内部状态生成 comfort noise</summary>
    public float[]? DecodePlc(float volume)
    {
        try
        {
            var pcm = new short[_frameSize];
            int n;
            lock (_decoderLock)
            {
#pragma warning disable CS0618
                n = _decoder.Decode(null, 0, 0, pcm, 0, _frameSize, false);
#pragma warning restore CS0618
            }
            return n > 0 ? ShortToFloat(pcm, volume) : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// FEC 序号追踪：检测 seq 跳号。
    /// 返回需要 FEC 恢复的 seq（null = 无需恢复）。
    /// </summary>
    public uint? TrackSequence(uint seq)
    {
        uint? fecSeq = null;
        if (_hasLastSeq)
        {
            uint gap = SequenceHelper.Distance(_lastSeq, seq);
            if (gap == 2)
            {
                fecSeq = seq - 1;
            }
            else if (gap > 2 && gap < 1000)
            {
                _lostFrames += gap - 1;
            }
        }
        _lastSeq = seq;
        _hasLastSeq = true;
        return fecSeq;
    }

    /// <summary>重置 FEC 追踪状态（新会话时调用）</summary>
    public void ResetTracking()
    {
        _hasLastSeq = false;
        _lastSeq = 0;
        _lostFrames = 0;
        _fecRecovered = 0;
    }

    // ── 内部 ──

    private short[]? DecodeShort(byte[] opus, bool fec)
    {
        try
        {
            var pcm = new short[_frameSize];
#pragma warning disable CS0618
            int n = _decoder.Decode(opus, 0, opus.Length, pcm, 0, _frameSize, fec);
#pragma warning restore CS0618
            return n > 0 ? pcm : null;
        }
        catch (Exception ex)
        {
            Log.E("OpusDecode", $"Decode error (opus len={opus.Length}, fec={fec}): {ex.Message}");
            return null;
        }
    }

    private static float[] ShortToFloat(short[] pcmShort, float volume)
    {
        var pcmFloat = new float[pcmShort.Length];
        for (int i = 0; i < pcmShort.Length; i++)
            pcmFloat[i] = pcmShort[i] / 32768f * volume;
        return pcmFloat;
    }
}
