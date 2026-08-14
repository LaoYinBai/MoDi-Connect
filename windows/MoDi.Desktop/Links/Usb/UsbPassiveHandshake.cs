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
using MoDi.Desktop.Core.Session;
using MoDi.Core;
using MoDi.Protocol;
using MoDi.Core.Adapters;
using MoDi.Core.Factory;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop.Links;

/// <summary>
/// UsbPassiveHandshake — USB 链路被动握手 + ROUTE 热切处理
///
/// 职责：
///   1. 等待 Android HELLO → 校验 token → 回 HELLO_ACK(route)
///   2. 处理推流中 ROUTE 包 → 切换 AudioRouter 模式
///
/// 与 BtPassiveHandshake 完全对称（仅 LinkTypeId 不同）。
/// </summary>
internal static class UsbPassiveHandshake
{
    private const string Tag = "UsbHandshake";
    private const string UsbToken = "MODI";  // 必须 ≤ 8 字符（payload 限制）
    private const int HelloTimeoutMs = 60_000;

    /// <summary>
    /// 等待 Android HELLO 并完成被动握手。
    /// 流程：注册 PacketReceived 回调 → 等待 HELLO 包 → 校验 token → 回 ACK/NACK。
    /// 返回 route（0-3），失败返回 -1。
    /// </summary>
    public static async Task<HandshakeResult?> WaitForHelloAsync(UsbTransport transport, CancellationToken ct)
    {
        var protocol = PlatformFactory.CreateProtocol();
        var tcs = new TaskCompletionSource<HandshakeResult?>();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(HelloTimeoutMs);
        using var reg = timeoutCts.Token.Register(() => tcs.TrySetResult(null));

        Action<ReadOnlyMemory<byte>> handler = data =>
        {
            var decoded = protocol.Decode(data.Span);
            if (!decoded.HasValue || decoded.Value.Type != PacketType.Hello) return;

            if (!HelloSessionPayload.TryDecode(decoded.Value.Payload, tokenRequired: true, out var identity) ||
                identity.Token != UsbToken)
            {
                Log.W(Tag, "Invalid session payload or token mismatch");
                var nack = new Packet { Type = PacketType.HelloNack, LinkType = LinkType.Usb, Sequence = 0, Payload = Array.Empty<byte>() };
                _ = transport.SendAsync(protocol.Encode(nack));
                tcs.TrySetResult(null);
                return;
            }

            var ackPayload = HelloSessionPayload.Encode(identity.Route, null, identity.SessionId);
            var ack = new Packet { Type = PacketType.HelloAck, LinkType = LinkType.Usb, Sequence = 0, Payload = ackPayload };
            _ = transport.SendAsync(protocol.Encode(ack));
            Log.I(Tag, $"HELLO verified, ACK sent (route={identity.Route}, session={identity.SessionId})");
            tcs.TrySetResult(new HandshakeResult(identity.Route, identity.SessionId));
        };

        transport.PacketReceived += handler;
        try { return await tcs.Task; }
        finally { transport.PacketReceived -= handler; }
    }

    /// <summary>
    /// 处理 ROUTE 热切包。非 ROUTE 包直接忽略。
    /// 解码 payload[0] 为路线编号，映射到 AudioRouter 模式并切换。
    /// </summary>
    public static void HandleRoutePacket(
        ReadOnlyMemory<byte> data,
        AudioEngine? engine,
        Action<string>? onStatus,
        Action<int>? onRouteChanged = null)
    {
        var protocol = PlatformFactory.CreateProtocol();
        var decoded = protocol.Decode(data.Span);
        if (!decoded.HasValue || decoded.Value.Type != PacketType.Route) return;

        int route = decoded.Value.Payload.Length >= 1 ? Math.Clamp((int)decoded.Value.Payload[0], 0, 3) : 0;
        var mode = RouteToMode(route);

        engine?.Router.SetMode(mode);
        Log.I(Tag, $"Route hot-switch: route={route}, mode={mode}");
        onStatus?.Invoke($"USB：路线{route + 1}");
        onRouteChanged?.Invoke(route);
    }

    /// <summary>路线编号 → AudioRouter 模式（与其他链路映射一致）</summary>
    public static AudioRouter.RouteMode RouteToMode(int route) => route switch
    {
        0 => AudioRouter.RouteMode.SpeakerOnly,
        1 => AudioRouter.RouteMode.SpeakerOnly,
        2 => AudioRouter.RouteMode.MicOnly,
        3 => AudioRouter.RouteMode.MicOnlySys,
        _ => AudioRouter.RouteMode.SpeakerOnly,
    };
}
