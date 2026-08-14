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
using System.Threading;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop;

/// <summary>
/// PlaybackScheduler — 播放调度器
///
/// 职责：20ms 定时从 JitterBuffer 拉帧 → 冷启动淡入 → 写入 AudioRouter。
/// underrun 时调用 PLC 生成 comfort noise。每 5 秒输出诊断日志。
/// </summary>
public sealed class PlaybackScheduler : IDisposable
{
    private const int ColdStartFadeFrames = 100; // 淡入时长：100 帧 ≈ 2 秒
    private const int MaxPlcFrames = 25;         // 最多连续 25 帧 PLC（500ms）
    private const int FadeOutFrames = 3;         // 断流淡出：3 帧 ≈ 60ms（防爆音）

    private readonly JitterBuffer _jitterBuffer;
    private readonly AudioRouter _router;
    private readonly OpusDecodePipeline _decoder;
    private readonly Func<float> _volumeGetter;
    private readonly Func<(long recv, long audio, long decOk, long decFail)>? _diagGetter;

    private Timer? _playbackTimer;
    private volatile bool _running;

    // ── 冷启动淡入 ──
    private int _coldStartFrame;

    // ── 断流淡出 ──
    private int _fadeOutRemaining;
    private bool _wasPlaying;

    // ── PLC 计数 ──
    private int _consecutivePlc;

    // ── 诊断计数 ──
    private long _pullOkCount;
    private long _pullNullCount;
    private DateTime _diagLastLog = DateTime.UtcNow;

    /// <summary>获取 JitterBuffer 实例（诊断/统计用）</summary>
    public JitterBuffer Buffer => _jitterBuffer;
    /// <summary>取帧成功次数（用于诊断音频流畅度）</summary>
    public long PullOkCount => Interlocked.Read(ref _pullOkCount);
    /// <summary>取帧失败/underrun 次数（用于诊断音频卡顿）</summary>
    public long PullNullCount => Interlocked.Read(ref _pullNullCount);

    /// <summary>
    /// 创建播放调度器
    /// </summary>
    /// <param name="jitterBuffer">抖动缓冲实例，用于缓存乱序到达的音频帧</param>
    /// <param name="router">音频路由，将 PCM 帧输出到扬声器/线缆</param>
    /// <param name="decoder">Opus 解码管线，用于 underrun 时生成 comfort noise（PLC）</param>
    /// <param name="volumeGetter">音量获取委托，避免重复持有 AudioEngine 引用</param>
    /// <param name="diagGetter">可选诊断统计委托，来自 AudioEngine 的收发计数</param>
    public PlaybackScheduler(
        JitterBuffer jitterBuffer,
        AudioRouter router,
        OpusDecodePipeline decoder,
        Func<float> volumeGetter,
        Func<(long recv, long audio, long decOk, long decFail)>? diagGetter = null)
    {
        _jitterBuffer = jitterBuffer;
        _router = router;
        _decoder = decoder;
        _volumeGetter = volumeGetter;
        _diagGetter = diagGetter;
    }

    /// <summary>启动 20ms 定时播放循环，开始从 JitterBuffer 取帧播放</summary>
    public void Start()
    {
        _running = true;
        _playbackTimer = new Timer(_ => Tick(), null, 20, 20);
    }

    /// <summary>停止播放循环，释放定时器资源</summary>
    public void Stop()
    {
        _running = false;
        _playbackTimer?.Dispose();
        _playbackTimer = null;
    }

    /// <summary>重置冷启动淡入 + 淡出 + 诊断计数（新会话时）</summary>
    public void Reset()
    {
        _coldStartFrame = 0;
        _consecutivePlc = 0;
        _fadeOutRemaining = 0;
        _wasPlaying = false;
    }

    /// <summary>外部 Push 帧到 JitterBuffer</summary>
    public void Push(uint seq, float[] pcm)
    {
        _jitterBuffer.Push(seq, pcm);
    }

    /// <summary>JitterBuffer 重置（看门狗触发/新会话）</summary>
    public void ResetBuffer()
    {
        _jitterBuffer.Reset();
    }

    // ── 20ms 定时回调 ──

    private void Tick()
    {
        if (!_running) return;

        var pcm = _jitterBuffer.Pull();
        if (pcm != null)
        {
            Interlocked.Increment(ref _pullOkCount);
            _consecutivePlc = 0;
            _wasPlaying = true;

            // 冷启动淡入：前 2 秒音量从 0 线性渐变到 1
            if (_coldStartFrame < ColdStartFadeFrames)
            {
                float gain = (float)(_coldStartFrame + 1) / ColdStartFadeFrames;
                for (int i = 0; i < pcm.Length; i++)
                    pcm[i] *= gain;
                _coldStartFrame++;
            }

            _router.OnAudioFrame(pcm);
        }
        else if (_jitterBuffer.IsPrefilled && _consecutivePlc < MaxPlcFrames)
        {
            Interlocked.Increment(ref _pullNullCount);
            _consecutivePlc++;

            // 断流淡出：从音频→静音的过渡，防止爆音
            if (_wasPlaying)
            {
                _wasPlaying = false;
                _fadeOutRemaining = FadeOutFrames;
            }

            var plc = _decoder.DecodePlc(_volumeGetter());
            if (plc != null)
            {
                // 淡出期间逐渐降低音量
                if (_fadeOutRemaining > 0)
                {
                    float gain = (float)_fadeOutRemaining / FadeOutFrames;
                    for (int i = 0; i < plc.Length; i++)
                        plc[i] *= gain;
                    _fadeOutRemaining--;
                }
                _router.OnAudioFrame(plc);
            }
        }
        else
        {
            Interlocked.Increment(ref _pullNullCount);
        }

        // 每 5 秒输出一次诊断统计
        if ((DateTime.UtcNow - _diagLastLog).TotalSeconds >= 5)
        {
            _diagLastLog = DateTime.UtcNow;
            var (recv, audio, decOk, decFail) = _diagGetter?.Invoke() ?? (0, 0, 0, 0);
            Log.I("Playback",
                $"[Diag] recv={recv} audio={audio} decOk={decOk} decFail={decFail} " +
                $"pullOk={Interlocked.Read(ref _pullOkCount)} pullNull={Interlocked.Read(ref _pullNullCount)} " +
                $"jbCount={_jitterBuffer.Count} prefilled={_jitterBuffer.IsPrefilled} " +
                $"speakerReady={_router.IsSpeakerReady}");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
