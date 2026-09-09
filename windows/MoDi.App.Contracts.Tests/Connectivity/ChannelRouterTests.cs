using System.Text.Json;
using MoDi.App.Contracts.Connectivity;

namespace MoDi.App.Contracts.Tests.Connectivity;

public sealed class ChannelRouterTests
{
    [Fact]
    public async Task Same_audio_channel_id_is_isolated_by_session_and_owns_sequence()
    {
        var router = new ChannelRouter();
        using var vectors = JsonDocument.Parse(File.ReadAllText(FindVectors()));
        foreach (var item in vectors.RootElement.GetProperty("audioChannelSessions").EnumerateArray())
        {
            var plane = new MemoryChannelDataPlane(SessionId.Parse(item.GetProperty("session").GetString()!));
            router.Register(plane);
            await plane.OpenAsync(CancellationToken.None);
            foreach (var expected in item.GetProperty("sequences").EnumerateArray())
                Assert.Equal(expected.GetInt64(), plane.ReserveSequence());
            plane.ResetSequence();
            Assert.Equal(item.GetProperty("afterReset").GetInt64(), plane.ReserveSequence());
        }

        Assert.Equal(2, router.Channels.Count);
        Assert.All(router.Channels, channel => Assert.Equal(AudioChannel.Primary, channel.Descriptor.Id));
    }

    private sealed class MemoryChannelDataPlane(SessionId sessionId) : IChannelDataPlane
    {
        private long _next;
        public SessionId SessionId { get; } = sessionId;
        public ChannelDescriptor Descriptor { get; } = AudioChannel.Descriptor;
        public ChannelRuntimeState State { get; private set; } = ChannelRuntimeState.Closed;
        public long NextSequence => _next;
        public event Action<ReadOnlyMemory<byte>>? BytesReceived { add { } remove { } }
        public Task OpenAsync(CancellationToken cancellationToken) { State = ChannelRuntimeState.Open; ResetSequence(); return Task.CompletedTask; }
        public long ReserveSequence() => _next++;
        public void ResetSequence() => _next = 0;
        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken) { State = ChannelRuntimeState.Closed; return Task.CompletedTask; }
    }

    private static string FindVectors()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", "architecture", "connectivity-model-vectors.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate connectivity-model-vectors.json");
    }
}
