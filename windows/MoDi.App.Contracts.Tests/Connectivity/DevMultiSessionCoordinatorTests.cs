using MoDi.App.Contracts.Connectivity;

namespace MoDi.App.Contracts.Tests.Connectivity;

public sealed class DevMultiSessionCoordinatorTests
{
    [Fact]
    public async Task Default_disabled_coordinator_rejects_sessions_without_touching_resources()
    {
        await using var coordinator = new DevMultiSessionCoordinator();
        var transport = new MemoryTransportSession(TransportKind.Lan);
        var channel = new MemoryChannelDataPlane(SessionId.Parse("phone-a"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.StartAsync(SessionId.Parse("phone-a"), transport, channel, CancellationToken.None));

        Assert.Empty(coordinator.ActiveSessionIds);
        Assert.Equal(0, transport.CloseCount);
        Assert.Equal(0, channel.OpenCount);
    }

    [Fact]
    public async Task Independent_sessions_keep_state_sequence_and_resources_isolated()
    {
        await using var coordinator = new DevMultiSessionCoordinator(enabled: true);
        var sessions = new[] { "phone-a", "phone-b", "phone-c" }
            .Select(id => new SessionFixture(id))
            .ToArray();

        foreach (var fixture in sessions)
            await coordinator.StartAsync(fixture.Id, fixture.Transport, fixture.Channel, CancellationToken.None);

        Assert.Equal(3, coordinator.ActiveSessionIds.Count);
        Assert.All(sessions, fixture =>
        {
            Assert.Equal(0, fixture.Channel.ReserveSequence());
            Assert.Equal(1, fixture.Channel.ReserveSequence());
            Assert.True(coordinator.Channels.TryGet(fixture.Id, AudioChannel.Primary, out var routed));
            Assert.Same(fixture.Channel, routed);
        });

        Assert.True(coordinator.ObserveState(sessions[0].Id, SessionState.Reconnecting).Applied);
        Assert.Equal(SessionState.Reconnecting, coordinator.Sessions.Snapshots.Single(x => x.Id == sessions[0].Id).State);
        Assert.All(coordinator.Sessions.Snapshots.Where(x => x.Id != sessions[0].Id),
            snapshot => Assert.Equal(SessionState.Ready, snapshot.State));

        await coordinator.CloseSessionAsync(sessions[0].Id, CancellationToken.None);

        Assert.Equal(2, coordinator.ActiveSessionIds.Count);
        Assert.Equal(1, sessions[0].Transport.CloseCount);
        Assert.Equal(1, sessions[0].Transport.DisposeCount);
        Assert.Equal(1, sessions[0].Channel.CloseCount);
        Assert.All(sessions.Skip(1), fixture =>
        {
            Assert.Equal(0, fixture.Transport.CloseCount);
            Assert.Equal(ChannelRuntimeState.Open, fixture.Channel.State);
        });

        await coordinator.CloseAllAsync(CancellationToken.None);

        Assert.Empty(coordinator.ActiveSessionIds);
        Assert.All(sessions, fixture =>
        {
            Assert.Equal(1, fixture.Transport.CloseCount);
            Assert.Equal(1, fixture.Transport.DisposeCount);
            Assert.Equal(1, fixture.Channel.CloseCount);
        });
        Assert.All(coordinator.Sessions.Snapshots, snapshot => Assert.Equal(SessionState.Closed, snapshot.State));
    }

    private sealed class SessionFixture(string value)
    {
        public SessionId Id { get; } = SessionId.Parse(value);
        public MemoryTransportSession Transport { get; } = new(TransportKind.Lan);
        public MemoryChannelDataPlane Channel { get; } = new(SessionId.Parse(value));
    }

    private sealed class MemoryTransportSession(TransportKind kind) : ITransportSession
    {
        public TransportDescriptor Descriptor { get; } = TransportDescriptor.For(kind);
        public TransportSessionState State { get; private set; } = TransportSessionState.Connected;
        public int CloseCount { get; private set; }
        public int DisposeCount { get; private set; }
        public event Action<ReadOnlyMemory<byte>>? BytesReceived { add { } remove { } }
        public Task<IReadOnlyList<TransportCandidate>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TransportCandidate>>([]);
        public Task ListenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ConnectAsync(TransportEndpoint endpoint, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            State = TransportSessionState.Closed;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MemoryChannelDataPlane(SessionId sessionId) : IChannelDataPlane
    {
        private long _next;
        public SessionId SessionId { get; } = sessionId;
        public ChannelDescriptor Descriptor { get; } = AudioChannel.Descriptor;
        public ChannelRuntimeState State { get; private set; }
        public long NextSequence => _next;
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public event Action<ReadOnlyMemory<byte>>? BytesReceived { add { } remove { } }
        public Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            State = ChannelRuntimeState.Open;
            _next = 0;
            return Task.CompletedTask;
        }
        public long ReserveSequence() => _next++;
        public void ResetSequence() => _next = 0;
        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            State = ChannelRuntimeState.Closed;
            return Task.CompletedTask;
        }
    }
}
