using MoDi.App.Contracts.Connectivity;

namespace MoDi.App.Contracts.Tests.Connectivity;

public sealed class TransportRegistryTests
{
    [Fact]
    public void Unknown_transport_is_explicitly_unavailable_and_optional_failure_is_isolated()
    {
        var registry = new TransportRegistry();
        registry.Register(TransportKind.Lan, () => new StubSession(TransportKind.Lan));
        registry.Register(TransportKind.WifiDirect, () => throw new InvalidOperationException("platform unavailable"));

        Assert.False(registry.TryCreate(TransportKind.Usb, out _));
        Assert.Throws<InvalidOperationException>(() => registry.TryCreate(TransportKind.WifiDirect, out _));
        Assert.True(registry.TryCreate(TransportKind.Lan, out var lan));
        Assert.Equal(TransportKind.Lan, lan!.Descriptor.Kind);
    }

    [Fact]
    public void Duplicate_registration_is_rejected()
    {
        var registry = new TransportRegistry();
        registry.Register(TransportKind.Lan, () => new StubSession(TransportKind.Lan));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(TransportKind.Lan, () => new StubSession(TransportKind.Lan)));
    }

    private sealed class StubSession(TransportKind kind) : ITransportSession
    {
        public TransportDescriptor Descriptor { get; } = TransportDescriptor.For(kind);
        public TransportSessionState State => TransportSessionState.Idle;
        public event Action<ReadOnlyMemory<byte>>? BytesReceived { add { } remove { } }
        public Task<IReadOnlyList<TransportCandidate>> DiscoverAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TransportCandidate>>([]);
        public Task ListenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ConnectAsync(TransportEndpoint endpoint, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
