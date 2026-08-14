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
using System.Buffers;
using MoDi.Core;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop;

/// <summary>
/// 音频路由 — 将解码后的 PCM 分发到扬声器 / 虚拟麦克风。
///
/// P5 重构：不再直接管理 NAudio 设备，改为持有两个 IAudioRenderer 实例。
/// 路由逻辑（模式切换、淡入、增益）保留在此类。
///
/// 模式映射：
///   SpeakerOnly (模式1): 系统音频 → 扬声器
///   MicOnly     (模式2): 手机麦克风 → 虚拟麦克风
///   Both        (模式3): 混音 → 扬声器 + 虚拟麦克风
///   MicOnlySys  (模式4): 系统音频 → 虚拟麦克风
///
/// 线程安全：OnAudioFrame 可在任意线程调用；内部使用锁协调模式切换。
/// </summary>
public sealed class AudioRouter : IDisposable
{
    public enum RouteMode
    {
        SpeakerOnly,
        MicOnly,
        Both,
        MicOnlySys,
    }

    // ── 音频参数 ──
    private readonly AudioConfig _config;
    private const int FadeMs = 50;                   // 淡入时长（ms），防切模式时的爆音
    private const int FadeSamplesTotal = 48000 * FadeMs / 1000; // 2400 采样点
    private const float SpeakerGain = 1.0f;           // 扬声器增益

    // ── 渲染器（通过构造函数注入，由 PlatformFactory 创建） ──
    private readonly IAudioRenderer _speaker;
    private readonly IAudioRenderer _cable;

    // ── 运行时状态 ──
    private RouteMode _mode = RouteMode.SpeakerOnly;
    private readonly object _lock = new();
    private bool _disposed;
    private int _fadeSamplePos;

    // 复用增益缓冲区，减少 GC
    private float[]? _gainScratch;

    // 复用字节池，减少 GC（float[] → byte[] 转换用）
    private static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;

    /// <summary>麦克风输出状态变化时触发</summary>
    public Action<bool>? OnMicOutputChanged;
    /// <summary>错误通知</summary>
    public Action<string>? OnError;

    public RouteMode Mode => _mode;
    public bool IsOutputToMic => _mode is RouteMode.MicOnly or RouteMode.Both or RouteMode.MicOnlySys;
    public bool IsSpeakerReady { get { lock (_lock) return _speaker.IsReady; } }

    // ── 生命周期 ──

    /// <summary>
    /// 构造 AudioRouter，接收两个 IAudioRenderer 实例（由 PlatformFactory 创建）。
    /// </summary>
    /// <param name="speaker">扬声器渲染器</param>
    /// <param name="cable">CABLE Input 虚拟麦克风渲染器</param>
    /// <param name="config">音频配置</param>
    public AudioRouter(IAudioRenderer speaker, IAudioRenderer cable, AudioConfig? config = null)
    {
        _speaker = speaker;
        _cable = cable;
        _config = config ?? AudioConfig.Default;
    }

    /// <summary>启动扬声器输出（默认 SpeakerOnly 模式）。</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_speaker.IsReady) return;
            StartSpeaker();
        }
    }

    /// <summary>
    /// 切换路由模式。先停全部输出，再启动需要的（stop-start）。
    /// 返回 false 表示被拒绝（如缺少 CABLE 设备）。
    /// </summary>
    public bool SetMode(RouteMode newMode)
    {
        lock (_lock)
        {
            if (_disposed) return false;
            if (_mode == newMode && OutputsReady(newMode)) return true;

            var oldMode = _mode;
            bool wasMic = IsOutputToMic;

            // stop-start：先停全部，再启需要的
            _speaker.Stop();
            _cable.Stop();

            bool started = newMode switch
            {
                RouteMode.SpeakerOnly => EnsureSpeaker(),
                RouteMode.MicOnly => EnsureCable(),
                RouteMode.Both => EnsureSpeaker() && EnsureCable(),
                RouteMode.MicOnlySys => EnsureCable(),
                _ => false,
            };

            if (!started)
            {
                _speaker.Stop();
                _cable.Stop();
                _mode = oldMode;
                RestoreMode(oldMode);
                return false;
            }

            _mode = newMode;
            bool nowMic = IsOutputToMic;
            _fadeSamplePos = 0;

            if (wasMic != nowMic)
                OnMicOutputChanged?.Invoke(nowMic);

            return true;
        }
    }

    /// <summary>
    /// 接收一帧解码后的 PCM（float32 mono），由 AudioEngine 调用。线程安全。
    /// 根据当前模式路由到扬声器 / 虚拟麦克风 / 或同时输出
    /// </summary>
    public void OnAudioFrame(float[] pcm)
    {
        bool toSpeaker;
        bool toMic;

        lock (_lock)
        {
            if (_disposed) return;
            toSpeaker = _mode is RouteMode.SpeakerOnly or RouteMode.Both;
            toMic = IsOutputToMic;
            if (!toSpeaker && !toMic) return;
        }

        // ── 淡入处理 ──
        var processed = pcm;
        int fadePos;
        lock (_lock) { fadePos = _fadeSamplePos; }

        if (fadePos < FadeSamplesTotal)
        {
            int interleave = Math.Min(FadeSamplesTotal - fadePos, pcm.Length);
            processed = ApplyFade(pcm, fadePos, interleave);
            lock (_lock) { _fadeSamplePos += interleave; }
        }

        if (toSpeaker)
        {
            var gained = ApplyGain(processed);
            FeedRenderer(_speaker, gained);
        }

        if (toMic)
        {
            FeedRenderer(_cable, processed);
        }
    }

    /// <summary>停止所有输出。</summary>
    public void Stop()
    {
        lock (_lock)
        {
            _speaker.Stop();
            _cable.Stop();
            _fadeSamplePos = 0;
        }
    }

    /// <summary>
    /// 强制重启当前模式的音频输出（新会话时调用）。
    /// 解决 WaveOutEvent 在看门狗触发后 Stop 状态不恢复的问题。
    /// </summary>
    public void RestartOutput()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _fadeSamplePos = 0;

            // 强制 Stop→Play，确保 WaveOutEvent 重新进入播放状态
            switch (_mode)
            {
                case RouteMode.SpeakerOnly:
                    _speaker.Stop();
                    _speaker.Play();
                    break;
                case RouteMode.MicOnly:
                case RouteMode.MicOnlySys:
                    _cable.Stop();
                    _cable.Play();
                    break;
                case RouteMode.Both:
                    _speaker.Stop();
                    _speaker.Play();
                    _cable.Stop();
                    _cable.Play();
                    break;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _speaker.Release();
            _cable.Release();
        }
    }

    // ── 淡入 ──

    private static float[] ApplyFade(float[] pcm, int fadePos, int fadeLen)
    {
        var result = new float[pcm.Length];
        for (int i = 0; i < fadeLen; i++)
        {
            float t = (fadePos + i) / (float)FadeSamplesTotal;
            result[i] = pcm[i] * t;
        }
        for (int i = fadeLen; i < pcm.Length; i++)
            result[i] = pcm[i];
        return result;
    }

    // ── 增益 ──

    private float[] ApplyGain(float[] pcm)
    {
        if (Math.Abs(SpeakerGain - 1.0f) < 0.001f) return pcm;

        int len = pcm.Length;
        if (_gainScratch == null || _gainScratch.Length < len)
            _gainScratch = new float[len];

        for (int i = 0; i < len; i++)
        {
            float s = pcm[i] * SpeakerGain;
            if (s > 1.0f) s = 1.0f;
            else if (s < -1.0f) s = -1.0f;
            _gainScratch[i] = s;
        }
        return _gainScratch;
    }

    // ── 渲染器管理 ──

    private bool EnsureSpeaker()
    {
        if (_speaker.IsReady) { _speaker.Play(); return true; }
        return StartSpeaker();
    }

    private bool EnsureCable()
    {
        if (_cable.IsReady) { _cable.Play(); return true; }
        return StartCable();
    }

    private bool OutputsReady(RouteMode mode)
    {
        return mode switch
        {
            RouteMode.SpeakerOnly => _speaker.IsReady,
            RouteMode.MicOnly => _cable.IsReady,
            RouteMode.Both => _speaker.IsReady && _cable.IsReady,
            RouteMode.MicOnlySys => _cable.IsReady,
            _ => false,
        };
    }

    private void RestoreMode(RouteMode mode)
    {
        switch (mode)
        {
            case RouteMode.SpeakerOnly:
                EnsureSpeaker();
                break;
            case RouteMode.MicOnly:
            case RouteMode.MicOnlySys:
                EnsureCable();
                break;
            case RouteMode.Both:
                EnsureSpeaker();
                EnsureCable();
                break;
        }
    }

    private bool StartSpeaker()
    {
        if (!_speaker.Prepare(_config))
        {
            OnError?.Invoke("扬声器输出初始化失败");
            return false;
        }
        _speaker.Play();
        return true;
    }

    private bool StartCable()
    {
        if (!_cable.Prepare(_config))
        {
            OnError?.Invoke("VB-Audio Virtual Cable 未安装，请在 https://vb-audio.com/Cable/ 下载安装");
            return false;
        }
        _cable.Play();
        return true;
    }

    // ── float[] → byte[] 转换 + 喂入渲染器 ──

    private static void FeedRenderer(IAudioRenderer renderer, float[] pcm)
    {
        if (!renderer.IsReady) return;

        int byteLen = pcm.Length * sizeof(float);
        byte[] pooled = BytePool.Rent(byteLen);
        try
        {
            Buffer.BlockCopy(pcm, 0, pooled, 0, byteLen);
            renderer.FeedPcm(pooled, 0, byteLen);
        }
        finally
        {
            BytePool.Return(pooled);
        }
    }
}
