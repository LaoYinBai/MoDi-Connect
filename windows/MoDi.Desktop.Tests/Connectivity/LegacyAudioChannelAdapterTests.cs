using MoDi.App.Contracts.Connectivity;
using MoDi.Desktop.Connectivity.Channels;
using Xunit;

namespace MoDi.Desktop.Tests.Connectivity;

public sealed class LegacyAudioChannelAdapterTests
{
    [Fact]
    public async Task Adapter_preserves_wire_bytes_and_can_restart()
    {
        var transport = new FakeTransportSession();
        var channel = new LegacyAudioChannelAdapter(SessionId.Parse("session-a"), transport);
        ReadOnlyMemory<byte> received = default;
        channel.BytesReceived += bytes => received = bytes;

        await channel.OpenAsync(CancellationToken.None);
        await channel.SendAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
        transport.Emit(new byte[] { 4, 5, 6 });
        await channel.CloseAsync(CancellationToken.None);
        await channel.OpenAsync(CancellationToken.None);

        Assert.Equal(new byte[] { 1, 2, 3 }, transport.LastSent.ToArray());
        Assert.Equal(new byte[] { 4, 5, 6 }, received.ToArray());
        Assert.Equal(0, channel.ReserveSequence());
        Assert.False(AudioChannelComposition.IsEnabled(_ => null));
    }

    private sealed class FakeTransportSession : ITransportSession
    {
        public TransportDescriptor Descriptor => TransportDescriptor.For(TransportKind.Lan);
        public TransportSessionState State => TransportSessionState.Connected;
        public event Action<ReadOnlyMemory<byte>>? BytesReceived;
        public ReadOnlyMemory<byte> LastSent { get; private set; }
        public Task<IReadOnlyList<TransportCandidate>> DiscoverAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TransportCandidate>>([]);
        public Task ListenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ConnectAsync(TransportEndpoint endpoint, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) { LastSent = payload.ToArray(); return Task.CompletedTask; }
        public Task CloseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Emit(ReadOnlyMemory<byte> payload) => BytesReceived?.Invoke(payload);
    }
}
