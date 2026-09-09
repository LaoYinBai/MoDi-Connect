using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts.Connectivity;
using MoDi.Protocol;

namespace MoDi.Desktop.Connectivity.Channels;

/// <summary>Temporary compatibility façade allowing the unchanged AudioEngine to consume a Channel.</summary>
internal sealed class ChannelTransportAdapter : ITransport
{
    private readonly IChannelDataPlane _channel;

    internal ChannelTransportAdapter(IChannelDataPlane channel, TransportType type)
    {
        _channel = channel;
        Type = type;
        _channel.BytesReceived += OnBytesReceived;
    }

    public TransportType Type { get; }
    public bool IsConnected => _channel.State == ChannelRuntimeState.Open;
    public event Action<ReadOnlyMemory<byte>>? PacketReceived;
    public Task ConnectAsync(CancellationToken ct = default) => _channel.OpenAsync(ct);
    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => _channel.SendAsync(data, ct);
    public Task DisconnectAsync() => _channel.CloseAsync(CancellationToken.None);
    private void OnBytesReceived(ReadOnlyMemory<byte> payload) => PacketReceived?.Invoke(payload);
}

internal static class AudioChannelComposition
{
    internal static bool IsEnabled(Func<string, string?>? readSetting = null)
    {
        readSetting ??= Environment.GetEnvironmentVariable;
        return string.Equals(readSetting("MODI_AUDIO_CHANNEL"), "1", StringComparison.Ordinal);
    }
}
