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
using MoDi.Desktop.Core.Session;
using MoDi.Core.Factory;
using MoDi.Core.Infrastructure;
using MoDi.Desktop.Diagnostics;

namespace MoDi.Desktop.Links;

/// <summary>
/// WifiDirectLink — WiFi Direct P2P 链路（完整实现）
///
/// 职责：P2P 发现/连接 + QR 码 + 主动向 Android GO 发 HELLO 握手。
/// 与 LAN / 蓝牙 / USB 完全解耦。
/// </summary>
public sealed class WifiDirectLink : ILink
{
    private const string Tag = "WifiDirectLink";

    // ── 兼容别名；稳定值由 TransportIdentity 统一定义 ──
    public const int AudioPort = TransportIdentity.AudioPort;
    public const int HandshakePort = TransportIdentity.HandshakePort;

    // ── 核心模块 ──
    private WifiDirectP2pHelper? _p2pHelper;
    private readonly ConnectionStateManager _stateManager;
    private readonly Func<int, bool> _onHandshakeRoute;
    private CancellationTokenSource? _helloCts;  // 握手任务取消令牌

    // ── 事件（LinkManager / UI 订阅） ──
    public Action<string>? OnP2pStatusChanged;
    public Action<string?, string?>? OnQrChanged;
    public Action<bool>? OnP2pProgressVisible;
    public Action<bool, double>? OnP2pProgress;
    public Action<bool>? OnP2pActiveChanged;
    public Action<Guid>? OnSessionStarted;
    public Action<Guid>? OnSessionEnded;

    private bool _sessionActive;
    private Guid? _sessionId;

    public LinkState State { get; private set; } = LinkState.Idle;
    public bool IsActive => State != LinkState.Idle;

    public WifiDirectLink(
        ConnectionStateManager stateManager,
        Func<int, bool> onHandshakeRoute)
    {
        _stateManager = stateManager;
        _onHandshakeRoute = onHandshakeRoute;
    }

    // ── ILink 实现 ──

    /// <summary>启动 P2P 链路（创建 P2pHelper + 生成 QR 码 + 等待手机扫码连接）</summary>
    public async Task<bool> ConnectAsync()
    {
        if (State != LinkState.Idle) return true;
        State = LinkState.Listening;

        _p2pHelper = new WifiDirectP2pHelper();
        _p2pHelper.OnStatusChanged += msg =>
        {
            OnP2pProgressVisible?.Invoke(true);
            OnP2pStatusChanged?.Invoke(msg);
        };

        var qrContent = QrCodeHelper.BuildQrPayload(_p2pHelper.DeviceName, _p2pHelper.Token);
        OnQrChanged?.Invoke(qrContent, _p2pHelper.DeviceName);

        _helloCts = new CancellationTokenSource();
        var helloToken = _helloCts.Token;
        _p2pHelper.OnConnected += () => GlobalExceptionBoundary.Observe(
            Task.Run(() => SendHelloToAndroidGo(helloToken)),
            "WifiDirect.SendHello");
        _p2pHelper.OnDisconnected += EndSession;

        OnP2pActiveChanged?.Invoke(true);

        await _p2pHelper.StartAsync();
        return true;
    }

    /// <summary>停止 P2P 链路（取消握手 + 停止发现 + 清理 QR）</summary>
    public async Task DisconnectAsync()
    {
        if (_p2pHelper == null) return;

        // 先取消正在进行的握手任务
        _helloCts?.Cancel();
        _helloCts?.Dispose();
        _helloCts = null;

        await _p2pHelper.StopAsync();
        EndSession();
        _p2pHelper.Dispose();
        _p2pHelper = null;

        OnP2pActiveChanged?.Invoke(false);
        OnQrChanged?.Invoke(null, null);
        OnP2pProgressVisible?.Invoke(false);
        OnP2pStatusChanged?.Invoke("");
        OnP2pProgress?.Invoke(true, 0);
        State = LinkState.Idle;
    }

    // ── P2P 握手（主动向 Android GO 发 HELLO） ──
    // 流程：P2P 连接建立 → 等待 3s（Android 就绪）→ 发 HELLO(token) → 等 ACK → 设置路由

    private const int HelloInitialDelayMs = 3_000;   // 等 Android 端 waitForHello 就绪
    private const int HelloMaxAttempts = 6;           // 重试次数（覆盖 Android 60s 监听窗口）
    private const int HelloTimeoutMs = 3_000;         // 每次等待 ACK 超时
    private const int HelloRetryDelayMs = 2_000;      // 重试间隔

    /// <summary>向 Android GO 发送 HELLO 并等待 ACK（最多重试 6 次，覆盖 Android 60s 监听窗口）</summary>
    private async Task SendHelloToAndroidGo(CancellationToken ct)
    {
        ITransport? transport = null;
        try
        {
            var goIp = WifiDirectP2pHelper.GoIp;
            var token = _p2pHelper?.Token ?? "";

            // 等待 P2P 网络稳定 + Android 端 waitForHello 开始监听
            Log.I(Tag, $"P2P connected, waiting {HelloInitialDelayMs}ms before HELLO...");
            OnP2pStatusChanged?.Invoke("P2P 已连接，等待手机端就绪...");
            await Task.Delay(HelloInitialDelayMs, ct);

            Log.I(Tag, $"Sending HELLO to Android GO: {goIp}:{TransportIdentity.HandshakePort}");
            transport = PlatformFactory.CreateTransport(
                TransportType.Udp,
                goIp,
                TransportIdentity.HandshakePort);
            var protocol = PlatformFactory.CreateProtocol();
            await transport.ConnectAsync();

            var sessionId = Guid.NewGuid();
            var payload = HelloSessionPayload.Encode(0, token, sessionId);

            var packet = new Packet
            {
                Type = PacketType.Hello,
                LinkType = LinkType.WifiDirect,
                Sequence = 0,
                Payload = payload
            };
            var encoded = protocol.Encode(packet);

            for (int i = 0; i < HelloMaxAttempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                await transport.SendAsync(encoded);
                Log.I(Tag, $"HELLO sent to {goIp}:{TransportIdentity.HandshakePort} (attempt {i + 1}/{HelloMaxAttempts})");
                OnP2pStatusChanged?.Invoke($"正在握手...（{i + 1}/{HelloMaxAttempts}）");

                try
                {
                    var reply = await WaitForPacketAsync(transport, HelloTimeoutMs);
                    if (reply != null)
                    {
                        var decoded = protocol.Decode(reply.Value.Span);
                        if (decoded.HasValue &&
                            decoded.Value.Type == PacketType.HelloAck &&
                            HelloSessionPayload.MatchesAck(decoded.Value.Payload, sessionId))
                        {
                            HelloSessionPayload.TryDecode(
                                decoded.Value.Payload,
                                tokenRequired: false,
                                out var ackIdentity);
                            var route = ackIdentity.Route;

                            Log.I(Tag, $"HELLO_ACK received! P2P handshake OK, route={route}");
                            _stateManager.BeginConnecting();

                            if (!_sessionActive)
                            {
                                _sessionActive = true;
                                _sessionId = sessionId;
                                OnSessionStarted?.Invoke(sessionId);
                            }
                            _onHandshakeRoute(route);

                            // 配对持久化：握手成功即写入，后续冷启动免扫码
                            var paired = PairedDeviceStore.GetOrCreate();
                            paired.LastConnected = DateTime.Now;
                            PairedDeviceStore.Save(paired);

                            OnP2pStatusChanged?.Invoke($"P2P 握手成功 ✓ local={_p2pHelper?.LocalIp} go={goIp} route={route}");
                            OnP2pProgress?.Invoke(false, 100);
                            OnQrChanged?.Invoke(null, null);
                            _stateManager.Update(ConnectionState.Connected);
                            return;
                        }
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* timeout, retry */ }

                // 重试前等待
                if (i < HelloMaxAttempts - 1)
                    await Task.Delay(HelloRetryDelayMs, ct);
            }

            Log.W(Tag, $"P2P handshake failed: no HELLO_ACK after {HelloMaxAttempts} attempts");
            OnP2pStatusChanged?.Invoke("P2P 握手失败（手机未响应），等待重连...");
        }
        catch (OperationCanceledException)
        {
            Log.I(Tag, "HELLO task cancelled (P2P stopped)");
        }
        catch (Exception ex)
        {
            Log.E(Tag, $"SendHelloToAndroidGo error: {ex.Message}");
        }
        finally
        {
            if (transport != null) await transport.DisconnectAsync();
        }
    }

    /// <summary>等待收到一个数据包（带超时，用于等待 HELLO_ACK）</summary>
    private static async Task<ReadOnlyMemory<byte>?> WaitForPacketAsync(ITransport transport, int timeoutMs)
    {
        var tcs = new TaskCompletionSource<ReadOnlyMemory<byte>>();
        using var cts = new CancellationTokenSource(timeoutMs);
        using var reg = cts.Token.Register(() => tcs.TrySetCanceled());
        Action<ReadOnlyMemory<byte>> handler = data => tcs.TrySetResult(data);
        transport.PacketReceived += handler;
        try { return await tcs.Task; }
        catch (OperationCanceledException) { return null; }
        finally { transport.PacketReceived -= handler; }
    }

    public void Dispose()
    {
        EndSession();
        _p2pHelper?.Dispose();
    }

    private void EndSession()
    {
        if (!_sessionActive) return;
        _sessionActive = false;
        var endedSessionId = _sessionId;
        _sessionId = null;
        if (endedSessionId.HasValue) OnSessionEnded?.Invoke(endedSessionId.Value);
    }
}
