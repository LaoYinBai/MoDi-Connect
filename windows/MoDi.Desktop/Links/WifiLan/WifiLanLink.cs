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
using System.Threading.Tasks;
using MoDi.Core;
using MoDi.Protocol;
using MoDi.Core.Adapters;
using MoDi.Core.Factory;
using MoDi.Core.Infrastructure;
using MoDi.Desktop.Core.Session;

namespace MoDi.Desktop.Links;

internal interface IMdnsPublisher : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

internal sealed class MdnsPublisherAdapter(MdnsPublisher publisher) : IMdnsPublisher
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        publisher.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        publisher.StopAsync(cancellationToken);

    public void Dispose() => publisher.Dispose();
}

/// <summary>
/// WifiLanLink — WiFi LAN 链路（完整实现，常驻服务）
///
/// 职责：mDNS 发布 + HandshakeEndpoint + AudioEngine + 路由切换。
/// 与 WiFi Direct / 蓝牙 / USB 完全解耦。
///
/// 数据通路：UDP 12345 接收音频包 → AudioEngine（Opus 解码 → JitterBuffer → 播放）
/// 握手方向：Android 发 HELLO(route) → Windows 回 HELLO_ACK → 设置 AudioRouter 模式
/// 发现机制：mDNS 发布 _modi._udp 服务，Android 端扫描发现
/// </summary>
public sealed class WifiLanLink : ILink
{
    private const string Tag = "WifiLanLink";

    // ── 兼容别名；稳定值由 TransportIdentity 统一定义 ──
    public const int AudioPort = TransportIdentity.AudioPort;
    public const int HandshakePort = TransportIdentity.HandshakePort;
    public const string MdnsServiceType = TransportIdentity.MdnsServiceType;

    // ── 核心模块 ──
    private readonly IAudioEngine _engine;
    private readonly IMdnsPublisher _mdns;
    private readonly IHandshakeEndpoint _hs;
    private readonly AudioEngine? _concreteEngine;
    private readonly HandshakeEndpoint? _concreteHandshake;
    private readonly ConnectionStateManager _stateManager;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private bool _eventsSubscribed;
    private bool _disposed;

    // ── 事件（LinkManager / UI 订阅） ──
    public Action<string>? OnStatusChanged;
    public Action<ConnectionState>? OnStateChanged;
    public Action<int>? OnRouteChanged;
    public Action<Guid>? OnSessionStarted;

    // ── 公开属性 ──
    public LinkState State { get; private set; } = LinkState.Idle;
    public bool IsActive => State != LinkState.Idle;
    public AudioEngine Engine => _concreteEngine ??
        throw new InvalidOperationException("The injected audio engine is test-only.");

    /// <summary>暂停引擎（BT/USB 会话开始时由 LinkManager 调用）</summary>
    public void PauseEngine() => _engine.Stop();

    /// <summary>恢复引擎（BT/USB 会话结束时由 LinkManager 调用）</summary>
    public void ResumeEngine() => _engine.Start();
    public HandshakeEndpoint Handshake => _concreteHandshake ??
        throw new InvalidOperationException("The injected handshake endpoint is test-only.");
    public ConnectionStateManager StateManager => _stateManager;

    public float Volume
    {
        get => _engine.Volume;
        set => _engine.Volume = value;
    }

    /// <summary>构造函数：创建 UDP Transport(音频 12345 + 握手 12347) + AudioEngine + HandshakeEndpoint + mDNS 发布器</summary>
    public WifiLanLink(
        ConnectionStateManager stateManager,
        int audioPort = TransportIdentity.AudioPort,
        int handshakePort = TransportIdentity.HandshakePort)
    {
        _stateManager = stateManager;

        var audioTransport = PlatformFactory.CreateTransport(TransportType.Udp, null, audioPort);
        var hsTransport = PlatformFactory.CreateTransport(TransportType.Udp, null, handshakePort);
        var speakerRenderer = PlatformFactory.CreateRenderer(useCable: false);
        var cableRenderer = PlatformFactory.CreateRenderer(useCable: true);

        _concreteEngine = new AudioEngine(audioTransport, speakerRenderer, cableRenderer);
        _concreteHandshake = new HandshakeEndpoint(hsTransport, OnHandshakeRoute);
        _engine = _concreteEngine;
        _hs = _concreteHandshake;
        _mdns = new MdnsPublisherAdapter(MdnsPublisher.Create(Environment.MachineName, audioPort));
    }

    internal WifiLanLink(
        ConnectionStateManager stateManager,
        AudioEngine engine,
        HandshakeEndpoint handshake,
        MdnsPublisher mdns)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _concreteEngine = engine ?? throw new ArgumentNullException(nameof(engine));
        _concreteHandshake = handshake ?? throw new ArgumentNullException(nameof(handshake));
        _engine = engine;
        _hs = handshake;
        _mdns = new MdnsPublisherAdapter(mdns ?? throw new ArgumentNullException(nameof(mdns)));
    }

    internal WifiLanLink(
        ConnectionStateManager stateManager,
        IAudioEngine engine,
        IHandshakeEndpoint handshake,
        IMdnsPublisher mdns)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _hs = handshake ?? throw new ArgumentNullException(nameof(handshake));
        _mdns = mdns ?? throw new ArgumentNullException(nameof(mdns));
    }

    // ── ILink 实现 ──

    /// <summary>
    /// 启动 LAN 常驻服务（开机即启动，始终等待手机连接）
    /// 流程：订阅状态事件 → 启动 mDNS 发布 → 启动 HandshakeEndpoint → 启动 AudioEngine
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return false;
            if (State != LinkState.Idle)
                return true;

            State = LinkState.Listening;
            SubscribeEvents();
            _stateManager.ClearLastReason();

            try
            {
                await _mdns.StartAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _hs.Stop();
                _engine.Stop();
                UnsubscribeEvents();
                State = LinkState.Idle;
                _stateManager.Update(ConnectionState.Error, "mDNS 服务启动失败");
                OnStatusChanged?.Invoke($"mDNS 服务启动失败：{ex.Message}");
                return false;
            }

            _hs.Start();
            _engine.Start();

            OnStatusChanged?.Invoke("就绪：等待手机连接");
            _stateManager.Update(ConnectionState.Disconnected);
            return true;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>停止 LAN 常驻服务并回到 Idle；依赖保留以支持后续重连。</summary>
    public async Task DisconnectAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || State == LinkState.Idle)
                return;

            UnsubscribeEvents();
            _engine.Stop();
            _hs.Stop();
            await _mdns.StopAsync(CancellationToken.None).ConfigureAwait(false);
            State = LinkState.Idle;
            _stateManager.Update(ConnectionState.Disconnected);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void SubscribeEvents()
    {
        if (_eventsSubscribed)
            return;

        _stateManager.OnStateChanged += HandleStateChanged;
        _hs.OnHelloReceived += HandleHelloReceived;
        _engine.OnFirstFrameDecoded += HandleFirstFrameDecoded;
        _engine.OnAudioTimeout += HandleAudioTimeout;
        _engine.OnMicOutputChanged += HandleMicOutputChanged;
        _engine.OnError += HandleEngineError;
        _hs.OnError += HandleHandshakeError;
        _eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_eventsSubscribed)
            return;

        _stateManager.OnStateChanged -= HandleStateChanged;
        _hs.OnHelloReceived -= HandleHelloReceived;
        _engine.OnFirstFrameDecoded -= HandleFirstFrameDecoded;
        _engine.OnAudioTimeout -= HandleAudioTimeout;
        _engine.OnMicOutputChanged -= HandleMicOutputChanged;
        _engine.OnError -= HandleEngineError;
        _hs.OnError -= HandleHandshakeError;
        _eventsSubscribed = false;
    }

    private void HandleStateChanged(ConnectionState state) => OnStateChanged?.Invoke(state);

    private void HandleHelloReceived(HelloSessionIdentity identity)
    {
        State = LinkState.Connected;
        _stateManager.BeginConnecting();
        _stateManager.Update(ConnectionState.Connected);
        OnSessionStarted?.Invoke(identity.SessionId);
    }

    private void HandleFirstFrameDecoded()
    {
        State = LinkState.Streaming;
        _stateManager.Update(ConnectionState.Streaming);
    }

    private void HandleAudioTimeout()
    {
        if (_stateManager.State == ConnectionState.Streaming)
            _stateManager.Update(ConnectionState.Reconnecting);
    }

    private void HandleMicOutputChanged(bool toMic) =>
        OnStatusChanged?.Invoke(toMic
            ? "虚拟麦克风模式：音频已写入 CABLE Input，请在目标软件中选择 CABLE Output"
            : "扬声器模式：音频播放到系统默认扬声器");

    private void HandleEngineError(string message) => OnStatusChanged?.Invoke(message);

    private void HandleHandshakeError(string message) => OnStatusChanged?.Invoke(message);

    /// <summary>处理路由切换（供 WifiDirectLink P2P 握手成功后调用）</summary>
    public bool HandleRoute(int route) => OnHandshakeRoute(route);

    // ── 握手路由回调（收到 HELLO 或 ROUTE 包时触发，设置 AudioRouter 模式） ──

    /// <summary>
    /// 处理握手路由：重置会话 + 设置 AudioRouter 模式 + 更新状态机。
    /// 路线映射：0/1=扬声器，2=麦克风→CABLE，3=系统音频→CABLE
    /// </summary>

    private bool OnHandshakeRoute(int route)
    {
        route = Math.Clamp(route, 0, 3);
        _stateManager.BeginConnecting();

        var mode = route switch
        {
            0 => AudioRouter.RouteMode.SpeakerOnly,   // 系统音频 → 扬声器
            1 => AudioRouter.RouteMode.SpeakerOnly,   // 系统音频+麦克风混音 → 扬声器（混音在 Android 端完成，Windows 端只播放）
            2 => AudioRouter.RouteMode.MicOnly,        // 麦克风 → 虚拟麦克风
            3 => AudioRouter.RouteMode.MicOnlySys,     // 系统音频 → 虚拟麦克风
            _ => AudioRouter.RouteMode.SpeakerOnly,
        };

        _engine.ResetSession();

        if (!_engine.SetMode(mode))
        {
            Log.W(Tag, $"Route {route} rejected (CABLE not available?)");
            _stateManager.Update(ConnectionState.Error);
            OnStatusChanged?.Invoke($"路线 {route + 1} 切换失败");
            return false;
        }

        if (_stateManager.State != ConnectionState.Streaming)
            _stateManager.Update(ConnectionState.Connected);

        OnStatusChanged?.Invoke($"当前路线 {route + 1}：{ModeLabel(route)}");
        OnRouteChanged?.Invoke(route);
        return true;
    }

    /// <summary>路线编号 → 中文描述（UI 显示用）</summary>
    public static string ModeLabel(int route) => route switch
    {
        0 => "手机系统音频 → 电脑扬声器",
        1 => "手机系统音频 + 麦克风 → 电脑扬声器",
        2 => "手机麦克风 → 电脑虚拟麦克风",
        3 => "手机系统音频 → 电脑虚拟麦克风",
        _ => "未知路线",
    };

    public void Dispose()
    {
        _lifecycleGate.Wait();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            UnsubscribeEvents();
            State = LinkState.Idle;
            _engine.Dispose();
            _hs.Dispose();
            _mdns.Dispose();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }
}
