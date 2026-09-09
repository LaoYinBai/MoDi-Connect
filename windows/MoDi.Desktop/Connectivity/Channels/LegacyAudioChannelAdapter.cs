using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts.Connectivity;

namespace MoDi.Desktop.Connectivity.Channels;

/// <summary>Maps audio/primary to an approved legacy transport without inspecting or rewriting bytes.</summary>
internal sealed class LegacyAudioChannelAdapter : IChannelDataPlane
{
    private readonly ITransportSession _transport;
    private long _nextSequence;
    private bool _subscribed;

    internal LegacyAudioChannelAdapter(SessionId sessionId, ITransportSession transport)
    {
        SessionId = sessionId;
        _transport = transport;
    }

    public SessionId SessionId { get; }
    public ChannelDescriptor Descriptor => AudioChannel.Descriptor;
    public ChannelRuntimeState State { get; private set; } = ChannelRuntimeState.Closed;
    public long NextSequence => Interlocked.Read(ref _nextSequence);
    public event Action<ReadOnlyMemory<byte>>? BytesReceived;

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_subscribed) { _transport.BytesReceived += OnBytesReceived; _subscribed = true; }
        ResetSequence();
        State = ChannelRuntimeState.Open;
        return Task.CompletedTask;
    }

    public long ReserveSequence()
    {
        if (State != ChannelRuntimeState.Open) throw new InvalidOperationException("Channel is closed.");
        return Interlocked.Increment(ref _nextSequence) - 1;
    }

    public void ResetSequence() => Interlocked.Exchange(ref _nextSequence, 0);

    public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (State != ChannelRuntimeState.Open) throw new InvalidOperationException("Channel is closed.");
        return _transport.SendAsync(payload, cancellationToken);
    }

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_subscribed) { _transport.BytesReceived -= OnBytesReceived; _subscribed = false; }
        State = ChannelRuntimeState.Closed;
        return Task.CompletedTask;
    }

    private void OnBytesReceived(ReadOnlyMemory<byte> payload) => BytesReceived?.Invoke(payload);
}
