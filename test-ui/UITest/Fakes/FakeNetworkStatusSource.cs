using System;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeNetworkStatusSource : INetworkStatusSource
{
    public NetworkStatusSnapshot Snapshot { get; private set; } = new(
        "在家·LAN",
        "192.168.1.100",
        12345,
        12347,
        [
            new LinkStatusSnapshot(LinkKind.Lan, LinkAvailability.Active, "在家", "LAN 已连接"),
            new LinkStatusSnapshot(LinkKind.WifiDirect, LinkAvailability.Listening, "万能", "等待 P2P"),
            new LinkStatusSnapshot(LinkKind.Bluetooth, LinkAvailability.Inactive, "蓝牙", "等待启动"),
            new LinkStatusSnapshot(LinkKind.Usb, LinkAvailability.Listening, "USB", "等待设备")
        ]);

    public event Action<NetworkStatusSnapshot>? SnapshotChanged;

    public void Publish(NetworkStatusSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    public void Dispose()
    {
    }
}
