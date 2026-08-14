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
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MoDi.Core.Infrastructure;
using MoDi.Protocol;

namespace MoDi.Core.Adapters;

/// <summary>
/// UsbTransport — ITransport 的 TCP 实现（Windows 端，Client 角色）
///
/// 职责：通过 ADB forward 隧道连接 Android TCP Server，
/// 在字节流上实现 PacketHeader 帧分割（15B header + payload）。
///
/// 数据流：
///   Windows App (TCP Client) → localhost:12348 → [adb forward] → Android:12348 (TCP Server)
///   注意：adb forward 命令会让 adb 进程监听 Windows 12348 端口，
///   因此 Windows 端不能再绑定此端口，只能作为 Client 连接。
///
/// 帧分割协议（与 BluetoothTransport 一致）：
///   发送：直接写入 [15B header + payload] 完整字节数组
///   接收：读 15B → 解析 PayloadLength → 再读 payload → 触发 PacketReceived
///
/// 依赖：USB 链路专属，与 LAN/P2P/蓝牙完全解耦。
/// </summary>
public sealed class UsbTransport : ITransport, IDisposable
{
    private const string Tag = "UsbTransport";

    /// <summary>USB 链路 TCP 端口（adb forward 双端一致）</summary>
    public const int Port = 12348;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;
    private volatile bool _connected;

    // ── ITransport 实现 ──

    public event Action<ReadOnlyMemory<byte>>? PacketReceived;
    public bool IsConnected => _connected;
    public TransportType Type => TransportType.Usb;

    /// <summary>
    /// 连接到 Android TCP Server（localhost:12348，通过 adb forward 隧道）。
    /// 需先由 UsbDeviceHelper 执行 adb forward 建立隧道。
    /// 连接成功后自动启动帧分割读取循环。
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connected) return;

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync("127.0.0.1", Port, ct);
            _stream = _client.GetStream();
            _connected = true;

            _cts = new CancellationTokenSource();
            _readLoop = Task.Run(async () =>
            {
                await StreamFrameDecoder.RunLoopAsync(_stream, data => PacketReceived?.Invoke(data), msg => Log.W(Tag, msg), _cts.Token);
                _connected = false;
            });

            Log.I(Tag, $"TCP connected to localhost:{Port} (via adb forward)");
        }
        catch (Exception ex)
        {
            Log.E(Tag, $"Connect failed: {ex.Message}");
            _client?.Dispose();
            _client = null;
            throw;
        }
    }

    /// <summary>断开 TCP 连接，停止读取循环</summary>
    public async Task DisconnectAsync()
    {
        if (!_connected) return;
        _connected = false;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_readLoop != null)
        {
            try { await _readLoop; } catch { }
            _readLoop = null;
        }

        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;

        Log.I(Tag, "TCP disconnected");
    }

    /// <summary>发送数据包到 Android（通过 adb forward 隧道）</summary>
    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!_connected || _stream == null) return;

        try
        {
            await _stream.WriteAsync(data, ct);
            await _stream.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            Log.E(Tag, $"SendAsync error: {ex.Message}");
            _connected = false;
        }
    }

    public void Dispose()
    {
        _connected = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _stream?.Dispose();
        _client?.Dispose();
    }
}
