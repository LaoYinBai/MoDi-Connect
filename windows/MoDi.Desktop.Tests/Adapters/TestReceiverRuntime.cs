using MoDi.Desktop.Adapters;

namespace MoDi.Desktop.Tests.Adapters;

internal sealed class TestReceiverRuntime : IReceiverRuntime
{
    private Action? _snapshotChanged;
    private Action<string?, string?>? _qrPayloadChanged;

    public event Action? SnapshotChanged
    {
        add { _snapshotChanged += value; SnapshotSubscriberCount++; }
        remove { _snapshotChanged -= value; SnapshotSubscriberCount--; }
    }

    public event Action<string?, string?>? QrPayloadChanged
    {
        add { _qrPayloadChanged += value; QrSubscriberCount++; }
        remove { _qrPayloadChanged -= value; QrSubscriberCount--; }
    }

    public ConnectionState ConnectionState { get; set; } = ConnectionState.Idle;
    public string ActiveLink { get; set; } = "none";
    public int CurrentRoute { get; set; }
    public string StatusMessage { get; set; } = "就绪";
    public string LastError { get; set; } = string.Empty;
    public string LanStatus { get; set; } = "等待启动";
    public string P2pStatus { get; set; } = "等待启动";
    public string BluetoothStatus { get; set; } = "等待启动";
    public string UsbStatus { get; set; } = "等待启动";
    public bool IsP2pProgressVisible { get; set; }
    public bool IsP2pProgressIndeterminate { get; set; } = true;
    public double P2pProgress { get; set; }
    public double Volume { get; set; } = 0.75;
    public int InitializeCalls { get; private set; }
    public int RefreshP2pCalls { get; private set; }
    public int ConnectRecentP2pCalls { get; private set; }
    public int SnapshotSubscriberCount { get; private set; }
    public int QrSubscriberCount { get; private set; }
    public PairedDeviceStore.PairedInfo? RecentPair { get; set; }
    public Func<Task>? InitializeAction { get; set; }
    public Func<Task>? RefreshP2pAction { get; set; }
    public Func<Task>? ConnectRecentP2pAction { get; set; }

    public Task InitializeAsync()
    {
        InitializeCalls++;
        return InitializeAction?.Invoke() ?? Task.CompletedTask;
    }

    public Task RefreshP2pAsync()
    {
        RefreshP2pCalls++;
        return RefreshP2pAction?.Invoke() ?? Task.CompletedTask;
    }

    public Task ConnectRecentP2pAsync()
    {
        ConnectRecentP2pCalls++;
        return ConnectRecentP2pAction?.Invoke() ?? Task.CompletedTask;
    }

    public PairedDeviceStore.PairedInfo? GetRecentPair() => RecentPair;
    public void RaiseSnapshotChanged() => _snapshotChanged?.Invoke();
    public void RaiseQrPayloadChanged(string? payload, string? deviceName) =>
        _qrPayloadChanged?.Invoke(payload, deviceName);
}
