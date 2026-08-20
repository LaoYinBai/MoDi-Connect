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
using System.Threading;
using System.Threading.Tasks;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using MoDi.Desktop;
using MoDi.Desktop.Diagnostics;
using MoDi.Core.Infrastructure;
using MoDi.Protocol;

namespace MoDi.Core.Adapters;

/// <summary>
/// BluetoothTransport — ITransport 的 RFCOMM 实现（Windows 端，Server 角色）
///
/// 职责：使用 32feet.NET BluetoothListener 常驻监听 Android 连接，
/// 在字节流上实现 PacketHeader 帧分割（15B header + payload）。
///
/// 生命周期：
///   StartListening() → 常驻监听（后台线程）
///   WaitForConnectionAsync() → 等待手机连接
///   连接建立后自动开始帧分割读取循环
/// </summary>
public sealed class BluetoothTransport : ITransport, IDisposable
{
    private const string Tag = "BluetoothTransport";

    /// <summary>自定义服务 UUID（与 Windows transport identity 一致）</summary>
    public static readonly Guid ServiceUuid = TransportIdentity.BluetoothServiceUuid;

    private BluetoothListener? _listener;
    private BluetoothClient? _client;
    private Stream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;
    private volatile bool _connected;

    // ── ITransport 实现 ──

    public event Action<ReadOnlyMemory<byte>>? PacketReceived;
    public bool IsConnected => _connected;
    public TransportType Type => TransportType.Bluetooth;

    /// <summary>
    /// 启动 RFCOMM 监听（常驻，不阻塞）。
    /// 使用 32feet.NET BluetoothListener 注册 SDP 服务记录，
    /// Android 端可通过 UUID 发现并连接此服务。
    /// </summary>
    public void StartListening()
    {
        if (_listener != null) return;

        _listener = new BluetoothListener(TransportIdentity.BluetoothServiceUuid);
        _listener.Start();

        _cts = new CancellationTokenSource();
        Log.I(Tag, $"RFCOMM server listening (uuid={TransportIdentity.BluetoothServiceUuid})");
    }

    /// <summary>
    /// 等待 Android 连接（阻塞直到有连接或取消）。
    /// 连接建立后自动启动帧分割读取循环（ReadLoopAsync）。
    /// 每次新连接都会重置流状态，支持断开后重新等待。
    /// </summary>
    public async Task<bool> WaitForConnectionAsync(CancellationToken ct)
    {
        if (_listener == null) return false;

        try
        {
            Log.I(Tag, "Waiting for Android RFCOMM connection...");
            // 32feet.NET 的 AcceptBluetoothClientAsync 不支持 CancellationToken
            _client = await Task.Run(() => _listener.AcceptBluetoothClient(), ct);
            _stream = _client.GetStream();
            _connected = true;

            Log.I(Tag, $"Android connected: {_client.RemoteMachineName}");

            // 启动帧分割读取循环（使用共享 StreamFrameDecoder）
            var readCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                ct,
                _cts?.Token ?? CancellationToken.None);
            _readLoop = Task.Run(async () =>
            {
                try
                {
                    await StreamFrameDecoder.RunLoopAsync(
                        _stream,
                        data => PacketReceived?.Invoke(data),
                        msg => Log.W(Tag, msg),
                        readCancellation.Token);
                }
                finally
                {
                    readCancellation.Dispose();
                    _connected = false;
                }
            });
            return true;
        }
        catch (OperationCanceledException)
        {
            Log.I(Tag, "Accept cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Log.E(Tag, $"Accept error: {ex.Message}");
            return false;
        }
    }

    /// <summary>ITransport.ConnectAsync — 蓝牙 Server 模式不使用此方法（由 WaitForConnectionAsync 替代）。</summary>
    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>断开当前客户端连接（不停止监听，可继续等待下一个连接）</summary>
    public async Task DisconnectAsync()
    {
        _connected = false;
        var owner = _cts;
        var ownedCancellation = owner?.Token ?? CancellationToken.None;
        owner?.Cancel();

        if (_readLoop != null)
        {
            await TeardownObserver.AwaitAsync(
                _readLoop,
                ownedCancellation,
                "BT_READ_LOOP_STOPPED").ConfigureAwait(false);
            _readLoop = null;
        }

        owner?.Dispose();
        if (ReferenceEquals(_cts, owner))
            _cts = null;

        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;

        Log.I(Tag, "Client disconnected");
    }

    /// <summary>停止监听（关闭整个 Server，释放所有资源）</summary>
    public void StopListening()
    {
        _connected = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _listener?.Stop();
        _listener = null;
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        Log.I(Tag, "RFCOMM server stopped");
    }

    /// <summary>发送数据包到已连接的 Android 客户端</summary>
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
        StopListening();
    }
}
