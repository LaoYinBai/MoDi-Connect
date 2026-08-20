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
using MoDi.Desktop.Core.Session;
using MoDi.Core;
using MoDi.Protocol;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop;

internal interface IHandshakeEndpoint : IDisposable
{
    event Action<HelloSessionIdentity>? OnHelloReceived;
    event Action<string>? OnError;

    Func<SessionControlMessage, byte, SessionControlMessage>? OnDisconnectRequest { get; set; }

    void Start();
    void Stop();
}

/// <summary>
/// HandshakeEndpoint — 被动握手端点（监听 UDP 12347）
///
/// 职责：接收 HELLO/ROUTE 包并回复 ACK/NACK。
/// LAN/BT/USB 都是 Windows 等待 HELLO，不仅是 LAN 的 Server，
/// 因此命名为 Endpoint（端点）而非 Server。
///
/// - HELLO (type=0x01) → 回复 HELLO_ACK/HELLO_NACK，触发 OnHelloReceived + 路由切换
/// - ROUTE (type=0x04) → 热切路由（推流中切换路线，回复 ROUTE_ACK）
/// </summary>
public sealed class HandshakeEndpoint : IHandshakeEndpoint
{
    private readonly ITransport? _transport;
    private readonly IPacketProtocol _protocol = new PacketHeaderCodec();
    private volatile bool _running;
    private Func<int, bool>? _onModeChange;
    public event Action<string>? OnError;

    /// <summary>P2P 模式下的期望 Token（LAN 模式为 null 不校验）</summary>
    public string? ExpectedToken { get; set; }

    // ── ROUTE 防抖（快速切换只执行最后一次） ──
    private Timer? _routeDebounceTimer;
    private int _pendingRoute = -1;
    private readonly object _routeLock = new();
    private const int RouteDebounceMs = 150;

    /// <summary>收到 HELLO 握手请求时触发（仅 HELLO，不含 ROUTE 切换）</summary>
    public event Action<HelloSessionIdentity>? OnHelloReceived;
    public Func<SessionControlMessage, byte, SessionControlMessage>? OnDisconnectRequest { get; set; }

    /// <param name="transport">ITransport 实例（由 PlatformFactory 创建，端口 12347）</param>
    /// <param name="onModeChange">收到 HELLO/ROUTE 时触发，参数为路由模式 0-3</param>
    public HandshakeEndpoint(ITransport? transport = null, Func<int, bool>? onModeChange = null)
    {
        _transport = transport;
        _onModeChange = onModeChange;
    }

    // ── 生命周期 ──

    public void Start()
    {
        if (_running) return;

        if (_transport == null)
        {
            var msg = "握手端点未提供 ITransport，无法启动";
            Log.E("HandshakeEndpoint", msg);
            OnError?.Invoke(msg);
            return;
        }

        _running = true;
        _transport.PacketReceived += OnPacketReceived;
        _ = _transport.ConnectAsync();
    }

    public void Stop()
    {
        _running = false;
        if (_transport != null)
        {
            _transport.PacketReceived -= OnPacketReceived;
            _ = _transport.DisconnectAsync();
        }
    }

    public void Dispose() { Stop(); }

    // ── ROUTE 防抖逻辑 ──
    // 快速连续切换时，只执行最后一次（避免音频设备反复 stop-start 导致失效）
    private void DebouncedRouteChange(int newMode)
    {
        lock (_routeLock)
        {
            _pendingRoute = newMode;
            _routeDebounceTimer?.Dispose();
            _routeDebounceTimer = new Timer(_ => ApplyPendingRoute(), null, RouteDebounceMs, Timeout.Infinite);
        }
    }

    private void ApplyPendingRoute()
    {
        int mode;
        lock (_routeLock)
        {
            mode = _pendingRoute;
            _pendingRoute = -1;
        }
        if (mode >= 0)
        {
            _onModeChange?.Invoke(mode);
        }
    }

    // ── 数据包接收回调 ──
    // 通过 IPacketProtocol 统一解码，不直接调用 PacketHeader
    private void OnPacketReceived(ReadOnlyMemory<byte> data)
    {
        if (!_running) return;

        try
        {
            // 通过 IPacketProtocol 解码
            var packet = _protocol.Decode(data.Span);
            if (packet == null) return; // 校验失败，丢弃

            var type = packet.Value.Type;
            var payload = packet.Value.Payload;
            var linkType = packet.Value.LinkType;

            // ── HELLO — 手机端发起首次连接（payload: 1B routeMode [+ 8B token]） ──
            if (type == PacketType.Hello)
            {
                Log.I("HandshakeEndpoint", $"HELLO received: linkType={linkType}, payloadLen={payload.Length}, expectedToken={ExpectedToken ?? "(null)"}");

                var tokenRequired = ExpectedToken != null;
                if (!HelloSessionPayload.TryDecode(payload, tokenRequired, out var identity) ||
                    tokenRequired && identity.Token != ExpectedToken)
                {
                    var nack = new Packet { Type = PacketType.HelloNack, LinkType = linkType, Sequence = 0, Payload = Array.Empty<byte>() };
                    _ = _transport!.SendAsync(_protocol.Encode(nack));
                    Log.W("HandshakeEndpoint", "HELLO rejected: invalid session payload or token mismatch");
                    return;
                }

                bool accepted = _onModeChange?.Invoke(identity.Route) ?? true;
                if (accepted)
                    OnHelloReceived?.Invoke(identity);

                // 回复 HELLO_ACK 或 HELLO_NACK
                var replyType = accepted ? PacketType.HelloAck : PacketType.HelloNack;
                var replyPayload = accepted
                    ? HelloSessionPayload.Encode(identity.Route, null, identity.SessionId)
                    : Array.Empty<byte>();
                var replyPacket = new Packet { Type = replyType, LinkType = linkType, Sequence = 0, Payload = replyPayload };
                _ = _transport!.SendAsync(_protocol.Encode(replyPacket));
                Log.I("HandshakeEndpoint", $"HELLO reply sent: {replyType}, route={identity.Route}, session={identity.SessionId}");
            }
            // ── ROUTE — 推流中热切路线（payload: 1B newRouteMode） ──
            else if (type == PacketType.Route)
            {
                if (payload.Length >= 1)
                {
                    int newMode = Math.Clamp((int)payload[0], 0, 3);
                    DebouncedRouteChange(newMode);
                }

                // 回复 ROUTE_ACK 确认路由切换
                var ackPacket = new Packet { Type = PacketType.RouteAck, LinkType = linkType, Sequence = 0, Payload = Array.Empty<byte>() };
                _ = _transport!.SendAsync(_protocol.Encode(ackPacket));
            }
            else if (type == PacketType.Data &&
                SessionControlMessage.TryDecode(payload, out var control) &&
                control.Action == SessionControlAction.DisconnectRequest &&
                OnDisconnectRequest is { } handleDisconnect)
            {
                var ack = handleDisconnect(control, linkType);
                _ = _transport!.SendAsync(_protocol.Encode(ack.ToPacket()));
            }
        }
        catch (Exception ex)
        {
            Log.E("HandshakeEndpoint", $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
