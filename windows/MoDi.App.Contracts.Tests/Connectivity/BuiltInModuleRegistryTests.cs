using MoDi.App.Contracts.Connectivity;

namespace MoDi.App.Contracts.Tests.Connectivity;

public sealed class BuiltInModuleRegistryTests
{
    [Fact]
    public async Task Registered_capabilities_match_modules_and_one_failure_does_not_stop_another()
    {
        var sessionId = SessionId.Parse("phone-a");
        var sessionCore = ReadySession(sessionId);
        var audioHandled = 0;
        var audioStopped = 0;
        var clipboardStopped = 0;
        var audio = BuiltInModules.CreateAudio(
            (_, _, _) => { audioHandled++; return Task.CompletedTask; },
            stop: (_, _) => { audioStopped++; return Task.CompletedTask; });
        var clipboard = BuiltInModules.CreateClipboard(
            (_, _, _) => throw new InvalidOperationException("module failed"),
            stop: (_, _) => { clipboardStopped++; return Task.CompletedTask; });
        var registry = new BuiltInModuleRegistry([audio, clipboard]);
        var audioChannel = new MemoryChannelDataPlane(sessionId, AudioChannel.Descriptor);
        var clipboardChannel = new MemoryChannelDataPlane(sessionId, ClipboardChannel.Descriptor);

        Assert.Equal(
            [ConnectivityCapability.Audio, ConnectivityCapability.Clipboard],
            registry.RegisteredCapabilities);
        Assert.True((await registry.ActivateAsync(sessionId, "audio", audioChannel, CancellationToken.None)).Succeeded);
        Assert.True((await registry.ActivateAsync(sessionId, "clipboard", clipboardChannel, CancellationToken.None)).Succeeded);

        Assert.True((await registry.DispatchAsync(sessionId, "audio", new byte[] { 1, 2 }, CancellationToken.None)).Succeeded);
        var failed = await registry.DispatchAsync(sessionId, "clipboard", new byte[] { 3 }, CancellationToken.None);
        Assert.False(failed.Succeeded);
        Assert.Equal(BuiltInModuleState.Failed, failed.State);
        Assert.True((await registry.DispatchAsync(sessionId, "audio", new byte[] { 4 }, CancellationToken.None)).Succeeded);
        Assert.Equal(2, audioHandled);
        Assert.Equal(SessionState.Ready, sessionCore.Snapshots.Single().State);
        Assert.Equal(BuiltInModuleState.Ready,
            registry.Snapshots.Single(x => x.ModuleId == "audio").State);

        await registry.StopSessionAsync(sessionId, CancellationToken.None);

        Assert.Equal(1, audioStopped);
        Assert.Equal(1, clipboardStopped);
        Assert.Equal(1, audioChannel.CloseCount);
        Assert.Equal(1, clipboardChannel.CloseCount);
        Assert.All(registry.Snapshots, snapshot => Assert.Equal(BuiltInModuleState.Closed, snapshot.State));
    }

    [Fact]
    public async Task Missing_module_is_reported_without_changing_ready_session()
    {
        var sessionId = SessionId.Parse("phone-a");
        var sessionCore = ReadySession(sessionId);
        var registry = new BuiltInModuleRegistry([
            BuiltInModules.CreateAudio((_, _, _) => Task.CompletedTask),
        ]);

        var result = await registry.ActivateAsync(
            sessionId,
            "clipboard",
            new MemoryChannelDataPlane(sessionId, ClipboardChannel.Descriptor),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BuiltInModuleState.Unavailable, result.State);
        Assert.Equal(SessionState.Ready, sessionCore.Snapshots.Single().State);
        Assert.Equal([ConnectivityCapability.Audio], registry.RegisteredCapabilities);
    }

    private static SessionRegistry ReadySession(SessionId id)
    {
        var sessions = new SessionRegistry();
        Assert.True(sessions.ObserveStarted(id, TransportKind.Lan, SessionState.Ready).Applied);
        return sessions;
    }

    private sealed class MemoryChannelDataPlane(SessionId sessionId, ChannelDescriptor descriptor) : IChannelDataPlane
    {
        public SessionId SessionId { get; } = sessionId;
        public ChannelDescriptor Descriptor { get; } = descriptor;
        public ChannelRuntimeState State { get; private set; }
        public long NextSequence => 0;
        public int CloseCount { get; private set; }
        public event Action<ReadOnlyMemory<byte>>? BytesReceived { add { } remove { } }
        public Task OpenAsync(CancellationToken cancellationToken) { State = ChannelRuntimeState.Open; return Task.CompletedTask; }
        public long ReserveSequence() => 0;
        public void ResetSequence() { }
        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            State = ChannelRuntimeState.Closed;
            return Task.CompletedTask;
        }
    }
}
