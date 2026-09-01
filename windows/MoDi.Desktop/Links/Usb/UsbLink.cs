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
using MoDi.Desktop.Diagnostics;

namespace MoDi.Desktop.Links;

/// <summary>
/// UsbLink — USB 链路（Windows 端，常驻监听）
///
/// 职责：后台循环检测 USB 设备 → 自动 adb forward → TCP 连接手机 → 被动收 HELLO 回 ACK → AudioEngine 播放。
/// 与蓝牙链路对称：Windows 常驻等待，手机主动发起连接和握手。
///
/// 握手方向：Android 发 HELLO(token+route) → Windows 校验 → 回 HELLO_ACK(route)（与 LAN/蓝牙一致）
/// 数据通路：UsbTransport.PacketReceived → AudioEngine（Opus 解码 → 播放）
///
/// 依赖：USB 链路专属，与 LAN/P2P/蓝牙完全解耦。
/// 前置条件：USB 线连接 + 手机开启 USB 调试 + 系统 PATH 有 adb。
/// </summary>
public sealed class UsbLink : ILink
{
    private const string Tag = "UsbLink";

    // ── 链路常量 ──
    private const int DetectIntervalMs = 5_000;  // USB 设备检测轮询间隔

    // ── 核心模块 ──
    private UsbTransport? _transport;
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
    /// <summary>USB 会话开始（手机连接+握手成功），LinkManager 用于暂停 LAN 引擎</summary>
    public Action<Guid>? OnSessionStarted;
    /// <summary>USB 会话结束（手机断开），LinkManager 用于恢复 LAN 引擎</summary>
    public Action<Guid>? OnSessionEnded;
    public Func<SessionControlMessage, byte, SessionControlMessage>? OnDisconnectRequest;

    public LinkState State { get; private set; } = LinkState.Idle;
    public bool IsActive => _started;

    public UsbLink(ConnectionStateManager stateManager)
    {
        _stateManager = stateManager;
    }

    // ── ILink 实现 ──

    /// <summary>启动 USB 链路（常驻监听，与 LAN/蓝牙一起开机启动）</summary>
    public Task<bool> ConnectAsync()
    {
        if (_started) return Task.FromResult(true);
        _started = true;
        State = LinkState.Listening;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        // 启动常驻监听循环（后台）
        _listenLoop = Task.Run(() => ListenLoopAsync(ct));

        OnStatusChanged?.Invoke("USB：就绪，等待手机连接");
        OnActiveChanged?.Invoke(true);
        Log.I(Tag, "USB link started (resident)");
        return Task.FromResult(true);
    }

    /// <summary>停止 USB 链路（关闭监听 + 断开当前连接）</summary>
    public async Task DisconnectAsync()
    {
        if (!_started) return;
        _started = false;
        State = LinkState.Idle;

        var owner = _cts;
        var ownedCancellation = owner?.Token ?? CancellationToken.None;
        owner?.Cancel();

        if (_listenLoop != null)
        {
            await TeardownObserver.AwaitAsync(
                _listenLoop,
                ownedCancellation,
                "USB_LISTEN_LOOP_STOPPED").ConfigureAwait(false);
            _listenLoop = null;
        }

        owner?.Dispose();
        if (ReferenceEquals(_cts, owner))
            _cts = null;

        await CleanupSessionAsync();
        await UsbDeviceHelper.RemoveForwardAsync();

        OnStatusChanged?.Invoke("USB：已停止");
        OnActiveChanged?.Invoke(false);
    }

    // ── 常驻监听循环（核心状态机） ──
    // 流程：轮询检测 USB → adb forward → TCP Server 等待连接 → 被动握手 → 引擎播放 → 等待断开 → 循环

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 1. 轮询检测 USB 设备
                OnStatusChanged?.Invoke("USB：等待手机 USB 连接...");
                var hasDevice = await WaitForUsbDeviceAsync(ct);
                if (!hasDevice) continue;

                // 2. 建立 adb forward
                OnStatusChanged?.Invoke("USB：检测到设备，建立隧道...");
                var forwardOk = await UsbDeviceHelper.SetupForwardAsync(ct);
                if (!forwardOk)
                {
                    OnStatusChanged?.Invoke("USB：adb forward 失败，重试...");
                    await Task.Delay(DetectIntervalMs, ct);
                    continue;
                }

                // 3. TCP 连接手机（通过 adb forward 隧道）
                OnStatusChanged?.Invoke("USB：隧道就绪，等待手机连接...");
                _transport = new UsbTransport();
                await _transport.ConnectAsync(ct);

                // 4. 被动握手：等待手机 HELLO → 校验 → 回 ACK
                OnStatusChanged?.Invoke("USB：手机已连接，等待握手...");
                var handshake = await UsbPassiveHandshake.WaitForHelloAsync(_transport, ct);
                if (!handshake.HasValue)
                {
                    OnStatusChanged?.Invoke("USB：握手失败，重新等待...");
                    await CleanupSessionAsync();
                    continue;
                }

                // 5. 创建 AudioEngine → 播放
                _stateManager.BeginConnecting();
                var sessionId = handshake.Value.SessionId;
                OnSessionStarted?.Invoke(sessionId);
                StartAudioEngine(handshake.Value.Route);
                OnRouteChanged?.Invoke(handshake.Value.Route);
                OnStatusChanged?.Invoke($"USB：推流中 ✓ 路线{handshake.Value.Route + 1}");

                // 6. 监听 ROUTE 热切包
                _transport.PacketReceived += OnSessionPacket;


                // 7. 等待连接断开
                await WaitForDisconnectAsync(ct);

                // 8. 断开 → 清理 → 重新等待
                _transport.PacketReceived -= OnSessionPacket;
                OnSessionEnded?.Invoke(sessionId);
                OnStatusChanged?.Invoke("USB：手机断开，重新等待...");
                CleanupAudioEngine();
                await _transport.DisconnectAsync();
                _transport.Dispose();
                _transport = null;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log.E(Tag, $"ListenLoop error: {ex.Message}");
                if (!ct.IsCancellationRequested)
                    await Task.Delay(DetectIntervalMs, ct);
            }
        }
    }

    /// <summary>轮询等待 USB 设备连接（每 5 秒检测一次 adb devices）</summary>
    private async Task<bool> WaitForUsbDeviceAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (await UsbDeviceHelper.DetectDeviceAsync(ct))
                return true;
            await Task.Delay(DetectIntervalMs, ct);
        }
        return false;
    }

    // ── ROUTE 热切（命名方法，确保可取消订阅） ──

    private void OnSessionPacket(ReadOnlyMemory<byte> data)
    {
        var protocol = PlatformFactory.CreateProtocol();
        var decoded = protocol.Decode(data.Span);
        if (!decoded.HasValue) return;
        if (decoded.Value.Type == PacketType.Route)
        {
            UsbPassiveHandshake.HandleRoutePacket(data, _engine, OnStatusChanged, OnRouteChanged);
            return;
        }
        if (decoded.Value.Type != PacketType.Data ||
            !SessionControlMessage.TryDecode(decoded.Value.Payload, out var request) ||
            request.Action != SessionControlAction.DisconnectRequest ||
            OnDisconnectRequest is not { } handleDisconnect)
            return;

        var ack = handleDisconnect(request, LinkType.Usb);
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

    // ── AudioEngine 管理（USB 专属引擎，不复用 LAN 引擎） ──

    private void StartAudioEngine(int route)
    {
        var speaker = PlatformFactory.CreateRenderer(useCable: false);
        var cable = PlatformFactory.CreateRenderer(useCable: true);
        _engine = new AudioEngine(_transport, speaker, cable);
        _engine.Router.SetMode(UsbPassiveHandshake.RouteToMode(route));

        _engine.OnFirstFrameDecoded += () => _stateManager.Update(ConnectionState.Streaming);
        _engine.Start();
        _stateManager.Update(ConnectionState.Connected);
    }

    private void CleanupAudioEngine()
    {
        _engine?.Stop();
        _engine?.Dispose();
        _engine = null;
    }

    private async Task WaitForDisconnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _transport?.IsConnected == true)
            await Task.Delay(500, ct);
    }

    private async Task CleanupSessionAsync()
    {
        CleanupAudioEngine();
        if (_transport != null)
        {
            await _transport.DisconnectAsync();
            _transport.Dispose();
            _transport = null;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _engine?.Dispose();
        _transport?.Dispose();
    }
}
