using MoDi.Core;
using MoDi.Desktop.Core.Session;
using MoDi.Protocol;
using Xunit;

namespace MoDi.Desktop.Tests.Lifecycle;

public sealed class HandshakeReconnectTests
{
    [Theory]
    [InlineData(LinkType.WifiLan, null, true)]
    [InlineData(LinkType.WifiDirect, "TEST0001", true)]
    [InlineData(LinkType.WifiDirect, "WRONG001", false)]
    [InlineData(LinkType.WifiDirect, null, false)]
    public async Task Shared_endpoint_keeps_LAN_compatible_and_authenticates_P2P(byte link, string? token, bool accepted)
    {
        var transport = new MemoryTransport();
        using var endpoint = new HandshakeEndpoint(transport) { ExpectedToken = "TEST0001" };
        var protocol = new PacketHeaderCodec();
        var session = Guid.NewGuid();
        var observed = 0;
        endpoint.OnHelloReceived += _ => observed++;
        await endpoint.StartAsync(CancellationToken.None);
        transport.Emit(protocol.Encode(new Packet { Type=PacketType.Hello, LinkType=link, Sequence=0, Payload=HelloSessionPayload.Encode(2, token, session) }));
        var reply = protocol.Decode(transport.Reply.Span);
        Assert.True(reply.HasValue);
        Assert.Equal(accepted ? PacketType.HelloAck : PacketType.HelloNack, reply.Value.Type);
        Assert.Equal(accepted ? 1 : 0, observed);
        if(accepted) Assert.True(HelloSessionPayload.MatchesAck(reply.Value.Payload, session));
    }

    private sealed class MemoryTransport : ITransport
    {
        public event Action<ReadOnlyMemory<byte>>? PacketReceived;
        public TransportType Type => TransportType.Udp;
        public bool IsConnected => true;
        public ReadOnlyMemory<byte> Reply { get; private set; }
        public Task ConnectAsync(CancellationToken ct=default) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct=default) { Reply=data.ToArray(); return Task.CompletedTask; }
        public void Emit(ReadOnlyMemory<byte> bytes) => PacketReceived?.Invoke(bytes);
    }
}
