using Xunit;

namespace MoDi.Desktop.Tests.Transport;

public sealed class TransportIdentityTests
{
    [Fact]
    public void Stable_transport_identity_matches_1_0_contract()
    {
        Assert.Equal(12345, TransportIdentity.AudioPort);
        Assert.Equal(12347, TransportIdentity.HandshakePort);
        Assert.Equal("_modi._udp", TransportIdentity.MdnsServiceType);
        Assert.Equal(Guid.Parse("6D6F4469-0001-4000-8000-000000000001"),
            TransportIdentity.BluetoothServiceUuid);
    }
}
