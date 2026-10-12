using System.Net;
using System.Net.Sockets;
using MoDi.Core;
using MoDi.Desktop.Core.Session;
using MoDi.Protocol;
using Xunit;

namespace MoDi.Desktop.Tests.Connectivity;

public sealed class IndependentUdpSessionTests
{
    [Fact]
    public async Task Existing_protocol_keeps_sessions_distinct_on_two_independent_udp_sockets()
    {
        using var first = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var second = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var sender = new UdpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var codec = new PacketHeaderCodec();
        var firstSession=Guid.NewGuid(); var secondSession=Guid.NewGuid();
        var firstPacket=codec.Encode(new Packet { Type=PacketType.Hello, LinkType=LinkType.WifiLan, Sequence=0, Payload=HelloSessionPayload.Encode(0,null,firstSession) });
        var secondPacket=codec.Encode(new Packet { Type=PacketType.Hello, LinkType=LinkType.WifiLan, Sequence=0, Payload=HelloSessionPayload.Encode(2,null,secondSession) });
        await sender.SendAsync(firstPacket,(IPEndPoint)first.Client.LocalEndPoint!);
        await sender.SendAsync(secondPacket,(IPEndPoint)second.Client.LocalEndPoint!);
        var a=codec.Decode((await first.ReceiveAsync(timeout.Token)).Buffer);
        var b=codec.Decode((await second.ReceiveAsync(timeout.Token)).Buffer);
        Assert.True(HelloSessionPayload.TryDecode(a!.Value.Payload,false,out var firstIdentity));
        Assert.True(HelloSessionPayload.TryDecode(b!.Value.Payload,false,out var secondIdentity));
        Assert.Equal(firstSession,firstIdentity.SessionId);
        Assert.Equal(secondSession,secondIdentity.SessionId);
        first.Close();
        await sender.SendAsync(secondPacket,(IPEndPoint)second.Client.LocalEndPoint!);
        Assert.NotEmpty((await second.ReceiveAsync(timeout.Token)).Buffer);
    }

    [Fact]
    public void One_exclusive_listener_port_cannot_be_owned_by_two_sessions()
    {
        using var first=new UdpClient(); first.ExclusiveAddressUse=true;
        first.Client.Bind(new IPEndPoint(IPAddress.Loopback,0));
        using var second=new UdpClient(); second.ExclusiveAddressUse=true;
        Assert.Throws<SocketException>(() => second.Client.Bind(first.Client.LocalEndPoint!));
    }
}
