using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal static class SnapshotFactory
{
    public static NetworkStatusSnapshot Network() => new(
        CurrentLinkLabel: "在家·LAN",
        LocalIpAddress: "192.168.1.100",
        AudioPort: 12345,
        HandshakePort: 12347,
        Links:
        [
            new LinkStatusSnapshot(LinkKind.Lan, LinkAvailability.Active, "在家", "LAN 已连接"),
            new LinkStatusSnapshot(LinkKind.WifiDirect, LinkAvailability.Listening, "万能", "等待 P2P"),
            new LinkStatusSnapshot(LinkKind.Bluetooth, LinkAvailability.Inactive, "蓝牙", "等待启动"),
            new LinkStatusSnapshot(LinkKind.Usb, LinkAvailability.Listening, "USB", "等待设备")
        ]);

    public static PluginCatalogSnapshot Plugins() => new(
        Entries:
        [
            new PluginEntrySnapshot(
                "built-in-audio",
                "音频",
                IsBuiltIn: true,
                IsEnabled: true,
                CanUninstall: false,
                PluginHealth.BuiltIn,
                "内置插件 · 不可卸载",
                new PluginDeveloperMetadata("1.0.0", "MoDi", ["audio"]))
        ],
        CanImportExternal: true,
        CapabilityMessage: "支持 .NET DLL 与独立 EXE");

    public static PairingSnapshot Pairing(
        ReadOnlyMemory<byte> qrPng = default,
        DateTimeOffset? expiresAt = null,
        IReadOnlyList<PairedDeviceSnapshot>? devices = null,
        bool isRefreshing = false,
        string? errorCode = null,
        string? errorMessage = null) =>
        new(
            qrPng,
            DeviceName: "工作室电脑",
            expiresAt,
            devices ?? [new PairedDeviceSnapshot("recent-p2p", "工作室 Mac", "上次连接：今天")],
            isRefreshing,
            errorCode,
            errorMessage);

    public static ReceiverSnapshot Receiver(
        ReceiverState state = ReceiverState.Idle,
        double rms = 0,
        string status = "等待手机连接") =>
        new(
            state,
            status,
            LinkKind.None,
            Route: 0,
            RouteLabel: "系统音频 → 电脑扬声器",
            OutputDeviceLabel: "系统默认播放设备",
            Rms: rms,
            Links: [],
            IsP2pProgressVisible: false,
            IsP2pProgressIndeterminate: false,
            P2pProgress: 0,
            ErrorCode: null,
            ErrorMessage: null);
}
