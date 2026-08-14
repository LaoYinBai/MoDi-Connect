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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MoDi.Core.Infrastructure;
using MoDi.Protocol;

namespace MoDi.Core.Adapters;

/// <summary>
/// UdpTransport — ITransport 的 UDP 实现
///
/// 包裹 UdpClient，支持 server 模式（仅绑定端口）和 client 模式（指定远程主机）。
/// </summary>
public sealed class UdpTransport : ITransport, IDisposable
{
    private readonly UdpClient _client;
    private readonly string? _remoteHost;
    private readonly int _remotePort;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;
    private IPEndPoint? _lastRemoteEp;  // server 模式：记录最后收到包的远端地址

    public UdpTransport(int localPort, string? remoteHost = null, int? remotePort = null)
    {
        _client = new UdpClient(localPort);
        _remoteHost = remoteHost;
        _remotePort = remotePort ?? localPort;
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        if (_cts != null) return Task.CompletedTask;

        if (_remoteHost != null)
            _client.Connect(_remoteHost, _remotePort);

        _cts = new CancellationTokenSource();
        _receiveLoop = ReceiveLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        try
        {
            if (_remoteHost != null)
            {
                // client 模式：Connect 后直接 Send（零拷贝，无需 ToArray）
                await _client.SendAsync(data, ct);
            }
            else
            {
                // server 模式：回复给最后发包的远端
                var ep = _lastRemoteEp;
                if (ep != null)
                    await _client.SendAsync(data, ep, ct);
                else
                    Log.W("UdpTransport", "SendAsync called in server mode but no remote endpoint known yet");
            }
        }
        catch (Exception ex)
        {
            Log.E("UdpTransport", $"SendAsync error: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _client.ReceiveAsync(ct);
                _lastRemoteEp = result.RemoteEndPoint;  // 记录发送方地址（server 模式回复用）
                PacketReceived?.Invoke(result.Buffer);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { break; }
            catch (Exception ex)
            {
                Log.E("UdpTransport", $"ReceiveLoop error: {ex.Message}");
            }
        }
    }

    public event Action<ReadOnlyMemory<byte>>? PacketReceived;
    public bool IsConnected => _cts != null && !_cts.IsCancellationRequested;
    public MoDi.Protocol.TransportType Type => MoDi.Protocol.TransportType.Udp;

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _client.Dispose();
    }
}
