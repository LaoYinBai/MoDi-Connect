using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MoDi.Desktop.Links;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Connectivity.Receiver;

internal sealed class ReceiverLinkRuntime : IReceiverLinkRuntime
{
    private readonly LinkManager _links;
    private readonly Action<ConnectionState> _stateChanged;
    private readonly Action<string> _activeLinkChanged;
    private readonly Action<int> _routeChanged;
    private readonly Action<string> _lanStatusChanged;
    private readonly Action<string> _p2pStatusChanged;
    private readonly Action<string> _bluetoothStatusChanged;
    private readonly Action<string> _usbStatusChanged;
    private readonly Action<bool> _p2pProgressVisibleChanged;
    private readonly Action<bool, double> _p2pProgressChanged;
    private readonly Action<string?, string?> _qrChanged;
    private readonly Action<IReadOnlyList<WifiDirectCandidate>> _candidatesChanged;
    private bool _disposed;

    internal ReceiverLinkRuntime(LinkManager links)
    {
        _links = links ?? throw new ArgumentNullException(nameof(links));
        _stateChanged = state => ConnectionStateChanged?.Invoke(state);
        _activeLinkChanged = link => ActiveLinkChanged?.Invoke(link);
        _routeChanged = route => RouteChanged?.Invoke(route);
        _lanStatusChanged = message => LinkStatusChanged?.Invoke("lan", message);
        _p2pStatusChanged = message => LinkStatusChanged?.Invoke("wifi-direct", message);
        _bluetoothStatusChanged = message => LinkStatusChanged?.Invoke("bluetooth", message);
        _usbStatusChanged = message => LinkStatusChanged?.Invoke("usb", message);
        _p2pProgressVisibleChanged = visible => P2pProgressVisibleChanged?.Invoke(visible);
        _p2pProgressChanged = (indeterminate, value) => P2pProgressChanged?.Invoke(indeterminate, value);
        _qrChanged = (payload, name) => QrChanged?.Invoke(payload, name);
        _candidatesChanged = candidates => P2pCandidatesChanged?.Invoke(
            candidates.Select(candidate => new P2pCandidateInfo(candidate.DeviceId, candidate.DisplayName)).ToArray());

        _links.StateManager.OnStateChanged += _stateChanged;
        _links.ActiveLinkChanged += _activeLinkChanged;
        _links.RouteChanged += _routeChanged;
        _links.WifiLan.OnStatusChanged += _lanStatusChanged;
        _links.WifiDirect.OnP2pStatusChanged += _p2pStatusChanged;
        _links.Bluetooth.OnStatusChanged += _bluetoothStatusChanged;
        _links.Usb.OnStatusChanged += _usbStatusChanged;
        _links.WifiDirect.OnP2pProgressVisible += _p2pProgressVisibleChanged;
        _links.WifiDirect.OnP2pProgress += _p2pProgressChanged;
        _links.WifiDirect.OnQrChanged += _qrChanged;
        _links.WifiDirect.OnCandidatesChanged += _candidatesChanged;
    }

    public event Action<ConnectionState>? ConnectionStateChanged;
    public event Action<string>? ActiveLinkChanged;
    public event Action<int>? RouteChanged;
    public event Action<string, string>? LinkStatusChanged;
    public event Action<bool>? P2pProgressVisibleChanged;
    public event Action<bool, double>? P2pProgressChanged;
    public event Action<string?, string?>? QrChanged;
    public event Action<IReadOnlyList<P2pCandidateInfo>>? P2pCandidatesChanged;

    public double Volume
    {
        get => _links.Volume;
        set => _links.Volume = (float)value;
    }

    public Task<bool> StartLanAsync() => _links.StartLanAsync();
    public Task<bool> StartP2pAsync() => _links.StartP2pAsync();
    public Task StopP2pAsync() => _links.StopP2pAsync();
    public Task<bool> StartBluetoothAsync() => _links.StartBluetoothAsync();
    public Task<bool> StartUsbAsync() => _links.StartUsbAsync();
    public bool ConnectP2pCandidate(string deviceId) => _links.ConnectP2pCandidate(deviceId);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _links.StateManager.OnStateChanged -= _stateChanged;
        _links.ActiveLinkChanged -= _activeLinkChanged;
        _links.RouteChanged -= _routeChanged;
        _links.WifiLan.OnStatusChanged -= _lanStatusChanged;
        _links.WifiDirect.OnP2pStatusChanged -= _p2pStatusChanged;
        _links.Bluetooth.OnStatusChanged -= _bluetoothStatusChanged;
        _links.Usb.OnStatusChanged -= _usbStatusChanged;
        _links.WifiDirect.OnP2pProgressVisible -= _p2pProgressVisibleChanged;
        _links.WifiDirect.OnP2pProgress -= _p2pProgressChanged;
        _links.WifiDirect.OnQrChanged -= _qrChanged;
        _links.WifiDirect.OnCandidatesChanged -= _candidatesChanged;
        _links.Dispose();
    }
}
