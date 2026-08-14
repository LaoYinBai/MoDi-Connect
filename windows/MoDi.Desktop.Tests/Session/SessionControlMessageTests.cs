using MoDi.Desktop.Core.Session;
using MoDi.Protocol;
using Xunit;

namespace MoDi.Desktop.Tests.Session;

public sealed class SessionControlMessageTests
{
    private static readonly Guid SessionId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Fact]
    public void Disconnect_request_uses_stable_cross_platform_bytes()
    {
        var message = SessionControlMessage.Request(
            SessionId,
            LinkType.WifiLan,
            LinkType.Bluetooth,
            DisconnectReason.UserSwitch);

        Assert.Equal(
            Convert.FromHexString("0F01010100112233445566778899AABBCCDDEEFF01030100"),
            message.Encode());
        Assert.Equal(PacketType.Data, message.ToPacket().Type);
        Assert.Equal(LinkType.WifiLan, message.ToPacket().LinkType);
    }

    [Fact]
    public void Ack_preserves_the_request_session_identity()
    {
        var request = SessionControlMessage.Request(
            SessionId,
            LinkType.WifiDirect,
            LinkType.Usb,
            DisconnectReason.Repair);

        var ack = SessionControlMessage.Ack(request, DisconnectResult.Accepted);

        Assert.Equal(SessionControlAction.DisconnectAck, ack.Action);
        Assert.Equal(SessionId, ack.SessionId);
        Assert.Equal(LinkType.WifiDirect, ack.OldLink);
        Assert.Equal(LinkType.Usb, ack.TargetLink);
        Assert.Equal(DisconnectResult.Accepted, ack.Result);
        Assert.True(SessionControlMessage.TryDecode(ack.Encode(), out var decoded));
        Assert.Equal(ack, decoded);
    }

    [Fact]
    public void Malformed_disconnect_messages_are_rejected()
    {
        var valid = Convert.FromHexString("0F01010100112233445566778899AABBCCDDEEFF01030100");

        Assert.False(SessionControlMessage.TryDecode(valid.AsSpan(0, 23), out _));
        AssertRejected(valid, 0, 0x0E);
        AssertRejected(valid, 2, 0x02);
        AssertRejected(valid, 3, 0x7F);
        AssertRejected(valid, 20, 0x7F);
        AssertRejected(valid, 21, 0x7F);
    }

    [Fact]
    public void Hello_payload_keeps_route_token_and_RFC_4122_session_bytes()
    {
        var lan = Convert.FromHexString("0200112233445566778899AABBCCDDEEFF");
        var token = Convert.FromHexString("024D4F44490000000000112233445566778899AABBCCDDEEFF");

        Assert.Equal(lan, HelloSessionPayload.Encode(2, null, SessionId));
        Assert.Equal(token, HelloSessionPayload.Encode(2, "MODI", SessionId));
        Assert.True(HelloSessionPayload.TryDecode(lan, tokenRequired: false, out var lanIdentity));
        Assert.Equal(new HelloSessionIdentity(2, null, SessionId), lanIdentity);
        Assert.True(HelloSessionPayload.TryDecode(token, tokenRequired: true, out var tokenIdentity));
        Assert.Equal(new HelloSessionIdentity(2, "MODI", SessionId), tokenIdentity);
        Assert.False(HelloSessionPayload.TryDecode([4], tokenRequired: false, out _));
        Assert.False(HelloSessionPayload.TryDecode(new byte[25], tokenRequired: true, out _));
    }

    [Fact]
    public void Hello_ack_must_echo_the_expected_session_identity()
    {
        var ack = HelloSessionPayload.Encode(2, null, SessionId);

        Assert.True(HelloSessionPayload.MatchesAck(ack, SessionId));
        Assert.False(HelloSessionPayload.MatchesAck(ack, Guid.NewGuid()));
        Assert.False(HelloSessionPayload.MatchesAck([2], SessionId));
    }

    private static void AssertRejected(byte[] valid, int index, byte replacement)
    {
        var malformed = valid.ToArray();
        malformed[index] = replacement;
        Assert.False(SessionControlMessage.TryDecode(malformed, out _));
    }
}
