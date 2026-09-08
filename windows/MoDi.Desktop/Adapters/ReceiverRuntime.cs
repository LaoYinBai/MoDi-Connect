using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Adapters;

internal interface IReceiverRuntime
{
    event Action? SnapshotChanged;
    event Action<string?, string?>? QrPayloadChanged;
    ConnectionState ConnectionState { get; }
    string ActiveLink { get; }
    int CurrentRoute { get; }
    string StatusMessage { get; }
    string LastError { get; }
    string LanStatus { get; }
    string P2pStatus { get; }
    string BluetoothStatus { get; }
    string UsbStatus { get; }
    bool IsP2pProgressVisible { get; }
    bool IsP2pProgressIndeterminate { get; }
    double P2pProgress { get; }
    double Volume { get; set; }
    Task InitializeAsync();
    Task RefreshP2pAsync();
    Task ConnectRecentP2pAsync();
    Task ConnectP2pCandidateAsync(string deviceId);
    PairedDeviceStore.PairedInfo? GetRecentPair();
    IReadOnlyList<P2pCandidateInfo> GetP2pCandidates();
}

internal sealed class ReceiverRuntime(ReceiverController controller) : IReceiverRuntime
{
    private readonly ReceiverController _controller =
        controller ?? throw new ArgumentNullException(nameof(controller));

    public event Action? SnapshotChanged
    {
        add => _controller.SnapshotChanged += value;
        remove => _controller.SnapshotChanged -= value;
    }

    public event Action<string?, string?>? QrPayloadChanged
    {
        add => _controller.QrPayloadChanged += value;
        remove => _controller.QrPayloadChanged -= value;
    }

    public ConnectionState ConnectionState => _controller.ConnectionState;
    public string ActiveLink => _controller.ActiveLink;
    public int CurrentRoute => _controller.CurrentRoute;
    public string StatusMessage => _controller.StatusMessage;
    public string LastError => _controller.LastError;
    public string LanStatus => _controller.LanStatus;
    public string P2pStatus => _controller.P2pStatus;
    public string BluetoothStatus => _controller.BluetoothStatus;
    public string UsbStatus => _controller.UsbStatus;
    public bool IsP2pProgressVisible => _controller.IsP2pProgressVisible;
    public bool IsP2pProgressIndeterminate => _controller.IsP2pProgressIndeterminate;
    public double P2pProgress => _controller.P2pProgress;
    public double Volume { get => _controller.Volume; set => _controller.Volume = value; }
    public Task InitializeAsync() => _controller.InitializeAsync();
    public Task RefreshP2pAsync() => _controller.RefreshP2pAsync();
    public Task ConnectRecentP2pAsync() => _controller.ConnectRecentP2pAsync();
    public Task ConnectP2pCandidateAsync(string deviceId) => _controller.ConnectP2pCandidateAsync(deviceId);
    public PairedDeviceStore.PairedInfo? GetRecentPair() => _controller.GetRecentPair();
    public IReadOnlyList<P2pCandidateInfo> GetP2pCandidates() => _controller.GetP2pCandidates();
}
