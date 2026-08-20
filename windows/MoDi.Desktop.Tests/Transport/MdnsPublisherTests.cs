using Makaretu.Dns;
using Xunit;

namespace MoDi.Desktop.Tests.Transport;

public sealed class MdnsPublisherTests
{
    [Fact]
    public void Constructs_profile_with_stable_service_type()
    {
        string? capturedServiceType = null;
        Func<string, string, ushort, ServiceProfile> createProfile =
            (hostname, serviceType, port) =>
            {
                capturedServiceType = serviceType;
                return new ServiceProfile(hostname, serviceType, port);
            };

        using var publisher = new MdnsPublisher(
            "test-host",
            TransportIdentity.AudioPort,
            createProfile);

        Assert.Equal("_modi._udp", capturedServiceType);
    }
}
