using MoDi.Core.Adapters;
using MoDi.Core.Factory;
using MoDi.Protocol;
using Xunit;

namespace MoDi.Desktop.Tests.Protocol;

public sealed class FourLinkProtocolRegressionTests
{
    [Theory]
    [InlineData("LAN", LinkType.WifiLan, TransportType.Udp)]
    [InlineData("Wi-Fi Direct", LinkType.WifiDirect, TransportType.Udp)]
    [InlineData("Bluetooth", LinkType.Bluetooth, TransportType.Bluetooth)]
    [InlineData("USB", LinkType.Usb, TransportType.Usb)]
    public void Selected_link_preserves_binary_protocol_and_transport_mapping(
        string linkName,
        byte linkType,
        TransportType expectedTransport)
    {
        var codec = Assert.IsType<PacketHeaderCodec>(PlatformFactory.CreateProtocol());
        var hello = new Packet
        {
            Type = PacketType.Hello,
            LinkType = linkType,
            Sequence = 0,
            Payload = [0x11, 0x22],
        };

        var encodedHello = codec.Encode(hello);
        Assert.Equal(linkType, encodedHello[6]);
        Assert.Equal(17, encodedHello.Length);
        Assert.Equal(2, encodedHello[14]);
        AssertPacket(codec.Decode(encodedHello), PacketType.Hello, linkType, 0, [0x11, 0x22]);

        var ack = new Packet
        {
            Type = PacketType.HelloAck,
            LinkType = linkType,
            Sequence = uint.MaxValue,
            Payload = [],
        };
        var encodedAck = codec.Encode(ack);
        AssertPacket(codec.Decode(encodedAck), PacketType.HelloAck, linkType, uint.MaxValue, []);

        Assert.Null(codec.Decode(encodedHello.AsSpan(0, encodedHello.Length - 1)));
        Assert.True(SequenceHelper.Before(uint.MaxValue, 0));
        Assert.Equal(1u, SequenceHelper.Distance(uint.MaxValue, 0));

        var transport = CreateTransport(linkType);
        try
        {
            Assert.Equal(expectedTransport, transport.Type);
            Assert.Equal(linkName, LinkName(linkType));
        }
        finally
        {
            Assert.IsAssignableFrom<IDisposable>(transport).Dispose();
        }
    }

    private static ITransport CreateTransport(byte linkType) => linkType switch
    {
        LinkType.WifiLan or LinkType.WifiDirect => new UdpTransport(0),
        LinkType.Bluetooth => new BluetoothTransport(),
        LinkType.Usb => new UsbTransport(),
        _ => throw new ArgumentOutOfRangeException(nameof(linkType)),
    };

    private static string LinkName(byte linkType) => linkType switch
    {
        LinkType.WifiLan => "LAN",
        LinkType.WifiDirect => "Wi-Fi Direct",
        LinkType.Bluetooth => "Bluetooth",
        LinkType.Usb => "USB",
        _ => throw new ArgumentOutOfRangeException(nameof(linkType)),
    };

    private static void AssertPacket(
        Packet? actual,
        PacketType type,
        byte linkType,
        uint sequence,
        byte[] payload)
    {
        Assert.True(actual.HasValue);
        Assert.Equal(type, actual.Value.Type);
        Assert.Equal(linkType, actual.Value.LinkType);
        Assert.Equal(sequence, actual.Value.Sequence);
        Assert.Equal(payload, actual.Value.Payload);
    }
}
