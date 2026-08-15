using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.Protocol;

namespace MoDi.Desktop.Tests.TestDoubles;

/// <summary>
/// 测试用 ITransport 替身：不做任何网络 IO，可手动注入收到的包并记录发送内容。
/// </summary>
public sealed class LoopbackTransport : ITransport
{
    public event Action<ReadOnlyMemory<byte>>? PacketReceived;

    public int SendCount { get; private set; }

    public byte[]? LastSent { get; private set; }

    public bool IsConnected { get; private set; }

    public TransportType Type => TransportType.Udp;

    public Task ConnectAsync(CancellationToken ct = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        SendCount++;
        LastSent = data.ToArray();
        return Task.CompletedTask;
    }

    public void SimulateReceive(byte[] data) => PacketReceived?.Invoke(data);
}
