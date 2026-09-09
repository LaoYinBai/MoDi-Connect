using MoDi.App.Contracts.Connectivity;
using MoDi.Core;
using MoDi.Desktop.Connectivity.Lan;
using MoDi.Protocol;
using Xunit;

namespace MoDi.Desktop.Tests.Connectivity;

public sealed class LanTargetAudioTransportTests
{
    [Fact]
    public async Task Bound_session_routes_legacy_bytes_through_session_and_audio_channel()
    {
        var legacy = new FakeTransport();
        var target = new LanTargetAudioTransport(legacy);
        ReadOnlyMemory<byte> received = default;
        target.PacketReceived += bytes => received = bytes;
        await target.ConnectAsync();

        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        target.BindSession(sessionId);
        legacy.Emit(new byte[] { 4, 5, 6 });
        await target.SendAsync(new byte[] { 1, 2, 3 });
        target.ObserveStreaming();

        Assert.Equal(new byte[] { 4, 5, 6 }, received.ToArray());
        Assert.Equal(new byte[] { 1, 2, 3 }, legacy.LastSent.ToArray());
        Assert.Equal(SessionState.Streaming, target.CurrentSession?.State);
        Assert.Equal(ConnectivityCapability.Audio, Assert.Single(target.CurrentPeer!.Capabilities));
        Assert.Equal(AudioChannel.Primary, Assert.Single(target.Channels.Channels).Descriptor.Id);
    }

    [Fact]
    public async Task Rebinding_closes_the_old_session_and_keeps_channel_state_isolated()
    {
        var target = new LanTargetAudioTransport(new FakeTransport());
        await target.ConnectAsync();
        var first = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var second = Guid.Parse("22222222-2222-2222-2222-222222222222");

        target.BindSession(first);
        var firstChannel = Assert.Single(target.Channels.Channels);
        Assert.Equal(0, firstChannel.ReserveSequence());
        target.BindSession(second);
        var secondChannel = Assert.Single(target.Channels.Channels);

        Assert.Equal(ChannelRuntimeState.Closed, firstChannel.State);
        Assert.Equal(0, secondChannel.ReserveSequence());
        Assert.Equal(SessionState.Closed, target.Sessions.Snapshots.Single(x => x.Id.Value == first.ToString("N")).State);
        Assert.Equal(second.ToString("N"), target.CurrentSession?.Id.Value);
    }

    [Fact]
    public async Task P2p_fallback_removes_LAN_channel_and_forwards_legacy_bytes()
    {
        var legacy = new FakeTransport();
        var target = new LanTargetAudioTransport(legacy);
        ReadOnlyMemory<byte> received = default;
        target.PacketReceived += bytes => received = bytes;
        await target.ConnectAsync();
        target.BindSession(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        target.UseLegacyFallback();
        legacy.Emit(new byte[] { 7, 8, 9 });

        Assert.Empty(target.Channels.Channels);
        Assert.Equal(new byte[] { 7, 8, 9 }, received.ToArray());
        Assert.Null(target.CurrentPeer);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public void Feature_gate_is_explicit_and_defaults_off(string? value, bool expected) =>
        Assert.Equal(expected, LanTargetComposition.IsEnabled(_ => value));

    private sealed class FakeTransport : ITransport
    {
        public event Action<ReadOnlyMemory<byte>>? PacketReceived;
        public TransportType Type => TransportType.Udp;
        public bool IsConnected { get; private set; }
        public ReadOnlyMemory<byte> LastSent { get; private set; }
        public Task ConnectAsync(CancellationToken ct = default) { IsConnected = true; return Task.CompletedTask; }
        public Task DisconnectAsync() { IsConnected = false; return Task.CompletedTask; }
        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) { LastSent = data.ToArray(); return Task.CompletedTask; }
        public void Emit(ReadOnlyMemory<byte> data) => PacketReceived?.Invoke(data);
    }
}
