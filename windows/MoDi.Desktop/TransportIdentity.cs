using System;

namespace MoDi.Desktop;

public static class TransportIdentity
{
    public const int AudioPort = 12345;
    public const int HandshakePort = 12347;
    public const string MdnsServiceType = "_modi._udp";
    public static readonly Guid BluetoothServiceUuid =
        Guid.Parse("6D6F4469-0001-4000-8000-000000000001");
}
