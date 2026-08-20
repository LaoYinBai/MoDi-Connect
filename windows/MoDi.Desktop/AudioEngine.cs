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
using MoDi.Core;
using MoDi.Protocol;
using MoDi.Core.Infrastructure;
using MoDi.Desktop.Diagnostics;

namespace MoDi.Desktop;

/// <summary>
/// AudioEngine — 音频引擎编排层
///
/// 职责：生命周期管理（Start/Stop/ResetSession），协调子模块：
///   - OpusDecodePipeline（解码）
///   - PlaybackScheduler（播放调度）
///   - AudioWatchdog（断线检测）
///
/// 公开 API 签名不变，调用方零改动。
/// </summary>
public sealed class AudioEngine : IDisposable
{
    // ── 音频参数 ──
    private readonly AudioConfig _config;
    public const int SampleRate = 48000;
    public const int Channels = 1;
    public int FrameSize => _config.FrameSize;

    // ── 子模块 ──
    private readonly OpusDecodePipeline _decoder;
    private readonly PlaybackScheduler _scheduler;
    private readonly AudioWatchdog _watchdog;
    private readonly AudioRouter _router;

    // ── 网络 ──
    private readonly ITransport? _transport;
    private readonly IPacketProtocol _protocol = new PacketHeaderCodec();

    // ── 运行状态 ──
    private volatile bool _running;
    private long _frameCount;
    private float _volume = 1.0f;

    // ── 诊断 ──
    private long _pktRecvCount;
    private long _audioPktCount;
    private long _decodeOkCount;
    private long _decodeFailCount;

    /// <summary>解码出第一帧音频时触发（仅一次），用于 ConnectionState 状态机</summary>
    public event Action? OnFirstFrameDecoded;

    /// <summary>音频超时触发（连续 N ms 未收到音频），用于进入 RECOVERING</summary>
    public event Action? OnAudioTimeout;

    // ── 公开属性 ──
    public AudioRouter Router => _router;

    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0, 2);
    }

    /// <summary>构造 AudioEngine，接收 AudioConfig 参数、ITransport 实例和两个 IAudioRenderer</summary>
    public AudioEngine(ITransport? transport = null, IAudioRenderer? speaker = null, IAudioRenderer? cable = null, AudioConfig? config = null)
    {
        _config = config ?? AudioConfig.Default;
        _transport = transport;

        _router = new AudioRouter(
            speaker ?? new MoDi.Core.Adapters.SpeakerRenderer(),
            cable ?? new MoDi.Core.Adapters.CableRenderer(),
            _config);

        _decoder = new OpusDecodePipeline(SampleRate, Channels, FrameSize);
        _scheduler = new PlaybackScheduler(
            new JitterBuffer(capacity: 24, preFillCount: 5),
            _router, _decoder, () => _volume,
            () => (Interlocked.Read(ref _pktRecvCount), Interlocked.Read(ref _audioPktCount),
                   Interlocked.Read(ref _decodeOkCount), Interlocked.Read(ref _decodeFailCount)));
        _watchdog = new AudioWatchdog(_config.AudioTimeoutMs);

        // 看门狗超时 → 重置 + 通知外部
        _watchdog.OnTimeout += () =>
        {
            Interlocked.Exchange(ref _frameCount, 0);
            _scheduler.ResetBuffer();
            _decoder.ResetTracking();
            OnAudioTimeout?.Invoke();
        };
    }

    // ── 生命周期 ──

    /// <summary>启动 UDP 监听 + 音频路由，Stop 后可重新 Start</summary>
    public void Start()
    {
        if (_running) return;

        if (_transport == null)
        {
            Log.E("AudioEngine", "No ITransport provided, cannot start.");
            return;
        }

        _running = true;
        _frameCount = 0;
        _decoder.ResetTracking();
        _scheduler.ResetBuffer();
        _scheduler.Reset();

        _watchdog.Start();
        _scheduler.Start();

        // Stop() 会停掉 Router 的全部输出；Start() 必须成对恢复，
        // 否则重连（AcceptSession 的 Pause→Resume）后解码正常但渲染器保持停止 → 无声。
        _router.RestartOutput();

        _transport.PacketReceived += OnPacketReceived;
        GlobalExceptionBoundary.Observe(
            _transport.ConnectAsync(),
            "AudioEngine.TransportConnect");
    }

    /// <summary>停止 UDP 监听 + 音频路由</summary>
    public void Stop()
    {
        _running = false;

        _watchdog.Stop();
        _scheduler.Stop();

        if (_transport != null)
        {
            _transport.PacketReceived -= OnPacketReceived;
            GlobalExceptionBoundary.Observe(
                _transport.DisconnectAsync(),
                "AudioEngine.TransportDisconnect");
        }

        _router.Stop();
    }

    /// <summary>
    /// 重置会话状态（收到新 HELLO 时调用）。
    /// Android 每次 HELLO 后会重启推流（seq 从 0 开始），
    /// 必须重置 JitterBuffer 和 seq 追踪，否则新会话帧会被当作“迟到包”丢弃。
    /// 同时强制重启音频输出，避免 WaveOutEvent 在看门狗触发后停顿不恢复。
    /// </summary>
    public void ResetSession()
    {
        _scheduler.ResetBuffer();
        _scheduler.Reset();
        _decoder.ResetTracking();
        _watchdog.Reset();
        Interlocked.Exchange(ref _frameCount, 0);
        _router.RestartOutput();
        Log.I("AudioEngine", "Session reset (new HELLO)");
    }

    public void Dispose()
    {
        Stop();
        _router.Dispose();
        _watchdog.Dispose();
        _scheduler.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── 数据包接收回调 ──

    private void OnPacketReceived(ReadOnlyMemory<byte> data)
    {
        if (!_running) return;

        Interlocked.Increment(ref _pktRecvCount);

        try
        {
            var packet = _protocol.Decode(data.Span);
            if (packet == null) return;
            if (packet.Value.Type != PacketType.Audio) return;
            Interlocked.Increment(ref _audioPktCount);

            var opusData = packet.Value.Payload;
            uint seq = packet.Value.Sequence;

            // FEC：检测跳号，恢复丢失帧
            var fecSeq = _decoder.TrackSequence(seq);
            if (fecSeq.HasValue)
            {
                var fecPcm = _decoder.DecodeFec(opusData, _volume);
                if (fecPcm != null)
                    _scheduler.Push(fecSeq.Value, fecPcm);
            }

            // 正常解码
            var pcm = _decoder.Decode(opusData, _volume);
            if (pcm != null)
            {
                Interlocked.Increment(ref _decodeOkCount);
                _watchdog.NotifyAudioReceived();
                _scheduler.Push(seq, pcm);

                if (Interlocked.Read(ref _frameCount) == 0)
                {
                    Log.I("AudioEngine", $"First decoded frame: seq={seq}, pcmLen={pcm.Length}");
                    OnFirstFrameDecoded?.Invoke();
                }
                Interlocked.Increment(ref _frameCount);
            }
            else
            {
                Interlocked.Increment(ref _decodeFailCount);
            }
        }
        catch (Exception ex)
        {
            Log.E("AudioEngine", $"OnPacketReceived error: {ex.Message}");
        }
    }
}
