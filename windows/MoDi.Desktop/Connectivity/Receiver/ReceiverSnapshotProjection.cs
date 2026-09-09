using System;
using System.Collections.Generic;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Connectivity.Receiver;

internal sealed class ReceiverSnapshotProjection : IDisposable
{
    private readonly IReceiverLinkRuntime _links;
    private bool _disposed;

    internal ReceiverSnapshotProjection(IReceiverLinkRuntime links)
    {
        _links = links ?? throw new ArgumentNullException(nameof(links));
        _links.ConnectionStateChanged += OnConnectionStateChanged;
        _links.ActiveLinkChanged += OnActiveLinkChanged;
        _links.RouteChanged += OnRouteChanged;
        _links.LinkStatusChanged += OnLinkStatusChanged;
        _links.P2pProgressVisibleChanged += OnP2pProgressVisibleChanged;
        _links.P2pProgressChanged += OnP2pProgressChanged;
        _links.QrChanged += OnQrChanged;
        _links.P2pCandidatesChanged += OnP2pCandidatesChanged;
    }

    internal event Action? Changed;
    internal event Action<string?, string?>? QrPayloadChanged;

    internal ConnectionState ConnectionState { get; private set; } = ConnectionState.Idle;
    internal string ActiveLink { get; private set; } = "none";
    internal int CurrentRoute { get; private set; }
    internal string StatusMessage { get; private set; } = "正在初始化接收服务...";
    internal string LastError { get; private set; } = "";
    internal string LanStatus { get; private set; } = "等待启动";
    internal string P2pStatus { get; private set; } = "等待启动";
    internal string BluetoothStatus { get; private set; } = "等待启动";
    internal string UsbStatus { get; private set; } = "等待启动";
    internal bool IsP2pProgressVisible { get; private set; }
    internal bool IsP2pProgressIndeterminate { get; private set; } = true;
    internal double P2pProgress { get; private set; }
    internal IReadOnlyList<P2pCandidateInfo> P2pCandidates { get; private set; } = [];
    internal double Volume { get => _links.Volume; set => _links.Volume = value; }

    internal void ApplyStartupResult(ReceiverStartupResult result)
    {
        StatusMessage = result.Links.Message;
        LastError = result.Links.Failed.Length > 0 ? result.Links.Message : "";
        if (result.P2pError is not null) ApplyP2pError(result.P2pError);
        Changed?.Invoke();
    }

    internal void BeginP2pRestart(string message)
    {
        P2pStatus = message;
        Changed?.Invoke();
    }

    internal void ApplyP2pError(string? error)
    {
        if (error is null) return;
        LastError = error;
        P2pStatus = error;
        Changed?.Invoke();
    }

    private void OnConnectionStateChanged(ConnectionState state)
    {
        ConnectionState = state;
        Changed?.Invoke();
    }

    private void OnActiveLinkChanged(string link)
    {
        ActiveLink = link;
        StatusMessage = StatusForActiveLink();
        Changed?.Invoke();
    }

    private void OnRouteChanged(int route)
    {
        CurrentRoute = route;
        Changed?.Invoke();
    }

    private void OnLinkStatusChanged(string link, string message)
    {
        switch (link)
        {
            case "lan": LanStatus = message; break;
            case "wifi-direct": P2pStatus = message; break;
            case "bluetooth": BluetoothStatus = message; break;
            case "usb": UsbStatus = message; break;
        }
        if (link == ActiveLink || IsError(message)) StatusMessage = message;
        if (IsError(message)) LastError = message;
        Changed?.Invoke();
    }

    private void OnP2pProgressVisibleChanged(bool visible)
    {
        IsP2pProgressVisible = visible;
        Changed?.Invoke();
    }

    private void OnP2pProgressChanged(bool indeterminate, double value)
    {
        IsP2pProgressIndeterminate = indeterminate;
        P2pProgress = value;
        Changed?.Invoke();
    }

    private void OnQrChanged(string? payload, string? deviceName) => QrPayloadChanged?.Invoke(payload, deviceName);

    private void OnP2pCandidatesChanged(IReadOnlyList<P2pCandidateInfo> candidates)
    {
        P2pCandidates = candidates;
        Changed?.Invoke();
    }

    private string StatusForActiveLink() => ActiveLink switch
    {
        "none" => "当前无活跃链路",
        "wifi-direct" => P2pStatus,
        "bluetooth" => BluetoothStatus,
        "usb" => UsbStatus,
        _ => LanStatus,
    };

    private static bool IsError(string message) =>
        message.Contains("错误", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("失败", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _links.ConnectionStateChanged -= OnConnectionStateChanged;
        _links.ActiveLinkChanged -= OnActiveLinkChanged;
        _links.RouteChanged -= OnRouteChanged;
        _links.LinkStatusChanged -= OnLinkStatusChanged;
        _links.P2pProgressVisibleChanged -= OnP2pProgressVisibleChanged;
        _links.P2pProgressChanged -= OnP2pProgressChanged;
        _links.QrChanged -= OnQrChanged;
        _links.P2pCandidatesChanged -= OnP2pCandidatesChanged;
    }
}
