using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeReceiverStatusSource : IReceiverStatusSource
{
    public FakeReceiverStatusSource() => Snapshot = Create(ReceiverState.Idle, 0);

    public ReceiverSnapshot Snapshot { get; private set; }
    public int InitializeCalls { get; private set; }
    public event Action<ReceiverSnapshot>? SnapshotChanged;

    public Task<OperationResult> InitializeAsync(CancellationToken cancellationToken)
    {
        InitializeCalls++;
        return Task.FromResult(OperationResult.Success());
    }

    public void SetState(ReceiverState state, double? rms = null) =>
        Publish(Create(state, rms ?? Snapshot.Rms));

    public void SetRms(double rms) => Publish(Snapshot with { Rms = Math.Clamp(rms, 0, 1) });

    public void Publish(ReceiverSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    public void Dispose()
    {
    }

    private static ReceiverSnapshot Create(ReceiverState state, double rms) => new(
        state,
        state switch
        {
            ReceiverState.Connected or ReceiverState.Streaming => "声音正在过桥",
            ReceiverState.Connecting => "正在握手",
            ReceiverState.Reconnecting => "正在重新连接",
            _ => "等待手机连接",
        },
        LinkKind.Lan,
        Route: 0,
        RouteLabel: "系统音频 → 电脑扬声器",
        OutputDeviceLabel: "系统默认播放设备",
        Rms: Math.Clamp(rms, 0, 1),
        Links:
        [
            new LinkStatusSnapshot(LinkKind.Lan, state is ReceiverState.Connected or ReceiverState.Streaming ? LinkAvailability.Active : LinkAvailability.Listening, "在家", "LAN 常驻监听"),
            new LinkStatusSnapshot(LinkKind.WifiDirect, LinkAvailability.Listening, "万能", "等待 P2P"),
            new LinkStatusSnapshot(LinkKind.Bluetooth, LinkAvailability.Inactive, "蓝牙", "等待启动"),
            new LinkStatusSnapshot(LinkKind.Usb, LinkAvailability.Listening, "USB", "等待设备")
        ],
        IsP2pProgressVisible: false,
        IsP2pProgressIndeterminate: false,
        P2pProgress: 0,
        ErrorCode: null,
        ErrorMessage: null);
}
