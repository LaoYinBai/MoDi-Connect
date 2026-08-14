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

/// <summary>
/// BluetoothLink — 蓝牙 RFCOMM 链路（常驻服务）
///
/// 职责：常驻监听 RFCOMM → 等待 Android 连接 → 被动接收 HELLO 回 ACK → AudioEngine 播放。
/// 与 WifiLanLink 对称：开机即启动，始终等待手机连接。
///
/// 数据通路：BluetoothTransport.PacketReceived → AudioEngine（Opus 解码 → 播放）
/// 握手方向：Android 发 HELLO(token) → Windows 校验 → 回 HELLO_ACK(route)（与 LAN 一致）
/// </summary>
public sealed class BluetoothLink : ILink
{
    private const string Tag = "BluetoothLink";

    // ── 核心模块 ──
    private BluetoothTransport? _transport;
    private AudioEngine? _engine;

    /// <summary>当前活跃的 AudioEngine（会话期间非 null，LinkManager 用于 Volume 控制）</summary>
    public AudioEngine? ActiveEngine => _engine;
    private readonly ConnectionStateManager _stateManager;
    private CancellationTokenSource? _cts;
    private Task? _listenLoop;
    private volatile bool _started;

    // ── 事件（LinkManager / UI 订阅） ──
    public Action<string>? OnStatusChanged;
    public Action<bool>? OnActiveChanged;
    public Action<int>? OnRouteChanged;
    /// <summary>蓝牙会话开始（手机连接+握手成功），LinkManager 用于暂停 LAN 引擎</summary>
    public Action<Guid>? OnSessionStarted;
    /// <summary>蓝牙会话结束（手机断开），LinkManager 用于恢复 LAN 引擎</summary>
    public Action<Guid>? OnSessionEnded;
    public Func<SessionControlMessage, byte, SessionControlMessage>? OnDisconnectRequest;

    public LinkState State { get; private set; } = LinkState.Idle;
    public bool IsActive => _started;

    public BluetoothLink(ConnectionStateManager stateManager)
    {
        _stateManager = stateManager;
    }

    // ── ILink 实现 ──

    /// <summary>启动蓝牙链路（常驻监听，与 LAN 一起开机启动）</summary>
    public Task<bool> ConnectAsync()
    {
        if (_started) return Task.FromResult(true);
        _started = true;
        State = LinkState.Listening;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        // 启动常驻监听循环（后台）
        _listenLoop = Task.Run(() => ListenLoopAsync(ct));

        OnStatusChanged?.Invoke("蓝牙：就绪，等待手机连接");
        OnActiveChanged?.Invoke(true);
        Log.I(Tag, "Bluetooth link started (resident)");
        return Task.FromResult(true);
    }

    /// <summary>停止蓝牙链路（关闭监听 + 断开当前连接）</summary>
    public async Task DisconnectAsync()
    {
        if (!_started) return;
        _started = false;
        State = LinkState.Idle;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_listenLoop != null)
        {
            try { await _listenLoop; } catch { }
            _listenLoop = null;
        }

        await CleanupSessionAsync();
        _transport?.StopListening();
        _transport?.Dispose();
        _transport = null;

        OnStatusChanged?.Invoke("蓝牙：已停止");
        OnActiveChanged?.Invoke(false);
    }

    // ── 常驻监听循环（核心状态机） ──
    // 流程：等待连接 → 被动握手 → 创建引擎播放 → 等待断开 → 清理 → 循环

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        _transport = new BluetoothTransport();
        _transport.StartListening();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 等待 Android 连接
                OnStatusChanged?.Invoke("蓝牙：等待手机连接...");
                var connected = await _transport.WaitForConnectionAsync(ct);
                if (!connected) continue;

                // 连接建立 → 等待 HELLO
                OnStatusChanged?.Invoke("蓝牙：手机已连接，等待握手...");
                var handshake = await BtPassiveHandshake.WaitForHelloAsync(_transport, ct);
                if (!handshake.HasValue)
                {
                    OnStatusChanged?.Invoke("蓝牙：握手失败，重新等待...");
                    await _transport.DisconnectAsync();
                    continue;
                }

                // 握手成功 → 创建 AudioEngine → 播放
                _stateManager.BeginConnecting();
                var sessionId = handshake.Value.SessionId;
                OnSessionStarted?.Invoke(sessionId);
                StartAudioEngine(handshake.Value.Route);
                OnRouteChanged?.Invoke(handshake.Value.Route);
                OnStatusChanged?.Invoke($"蓝牙：推流中 ✓ 路线{handshake.Value.Route + 1}");

                // 监听 ROUTE 热切包（AudioEngine 只处理 Audio 包，ROUTE 由此处理）
                _transport.PacketReceived += OnSessionPacket;

                // 等待连接断开（ReadLoop 退出 = 连接断开）
                await WaitForDisconnectAsync(ct);

                // 断开 → 清理 → 重新等待
                _transport.PacketReceived -= OnSessionPacket;
                OnSessionEnded?.Invoke(sessionId);
                OnStatusChanged?.Invoke("蓝牙：手机断开，重新等待...");
                CleanupAudioEngine();
                await _transport.DisconnectAsync();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log.E(Tag, $"ListenLoop error: {ex.Message}");
                if (!ct.IsCancellationRequested)
                    await Task.Delay(2000, ct);  // 防止快速循环
            }
        }
    }

    // ── ROUTE 热切（命名方法，确保可取消订阅） ──
    // 推流中手机切换路线时，发送 ROUTE 包，此处解码并切换 AudioRouter 模式

    private void OnSessionPacket(ReadOnlyMemory<byte> data)
    {
        var protocol = PlatformFactory.CreateProtocol();
        var decoded = protocol.Decode(data.Span);
        if (!decoded.HasValue) return;
        if (decoded.Value.Type == PacketType.Route)
        {
            BtPassiveHandshake.HandleRoutePacket(data, _engine, OnStatusChanged, OnRouteChanged);
            return;
        }
        if (decoded.Value.Type != PacketType.Data ||
            !SessionControlMessage.TryDecode(decoded.Value.Payload, out var request) ||
            request.Action != SessionControlAction.DisconnectRequest ||
            OnDisconnectRequest is not { } handleDisconnect)
            return;

        var ack = handleDisconnect(request, LinkType.Bluetooth);
        _ = AcknowledgeThenCloseAsync(protocol, ack);
    }

    private async Task AcknowledgeThenCloseAsync(IPacketProtocol protocol, SessionControlMessage ack)
    {
        var transport = _transport;
        if (transport == null) return;
        await transport.SendAsync(protocol.Encode(ack.ToPacket()));
        if (ack.Result == DisconnectResult.Accepted)
            await transport.DisconnectAsync();
    }

    internal void StopCurrentSession(bool closeTransport)
    {
        CleanupAudioEngine();
        if (closeTransport && _transport != null)
            _ = _transport.DisconnectAsync();
    }

    // ── AudioEngine 管理（蓝牙专属引擎，不复用 LAN 引擎） ──

    /// <summary>创建蓝牙专属 AudioEngine，设置路由模式并启动播放</summary>

    private void StartAudioEngine(int route)
    {
        var speaker = PlatformFactory.CreateRenderer(useCable: false);
        var cable = PlatformFactory.CreateRenderer(useCable: true);
        _engine = new AudioEngine(_transport, speaker, cable);
        _engine.Router.SetMode(BtPassiveHandshake.RouteToMode(route));

        _engine.OnFirstFrameDecoded += () => _stateManager.Update(ConnectionState.Streaming);
        _engine.Start();
        _stateManager.Update(ConnectionState.Connected);
    }

    /// <summary>停止并释放 AudioEngine</summary>
    private void CleanupAudioEngine()
    {
        _engine?.Stop();
        _engine?.Dispose();
        _engine = null;
    }

    /// <summary>轮询等待 RFCOMM 连接断开（ReadLoop 退出后 IsConnected 变 false）</summary>
    private async Task WaitForDisconnectAsync(CancellationToken ct)
    {
        // 等待 transport 连接断开（IsConnected 变 false）
        while (!ct.IsCancellationRequested && _transport?.IsConnected == true)
            await Task.Delay(500, ct);
    }

    /// <summary>清理当前会话（引擎 + 传输层）</summary>
    private async Task CleanupSessionAsync()
    {
        CleanupAudioEngine();
        if (_transport != null)
            await _transport.DisconnectAsync();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _engine?.Dispose();
        _transport?.Dispose();
    }
}
