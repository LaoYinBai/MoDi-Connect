/*
 * MoDi Connect - Cross-device interconnection protocol
 * Copyright (C) 2026 Silvite
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Threading.Tasks;
using MoDi.Desktop.Links;

namespace MoDi.Desktop.Services;

/// <summary>
/// Windows 接收端的 UI 门面。统一聚合链路事件，不暴露音频引擎内部对象。
/// </summary>
public sealed class ReceiverController : IDisposable
{
    private readonly LinkManager _linkManager = new();
    private readonly ReceiverInitialization _initialization = new();
    private bool _p2pStartingOrReady;

    public event Action? SnapshotChanged;
    public event Action<string?, string?>? QrPayloadChanged;

    public ConnectionState ConnectionState { get; private set; } = ConnectionState.Idle;
    public string ActiveLink { get; private set; } = "none";
    public int CurrentRoute { get; private set; }
    public string StatusMessage { get; private set; } = "正在初始化接收服务...";
    public string LastError { get; private set; } = "";
    public string LanStatus { get; private set; } = "等待启动";
    public string P2pStatus { get; private set; } = "等待启动";
    public string BluetoothStatus { get; private set; } = "等待启动";
    public string UsbStatus { get; private set; } = "等待启动";
    public bool IsP2pProgressVisible { get; private set; }
    public bool IsP2pProgressIndeterminate { get; private set; } = true;
    public double P2pProgress { get; private set; }
    public double Volume { get => _linkManager.Volume; set => _linkManager.Volume = (float)value; }

    public ReceiverController()
    {
        var lan = _linkManager.WifiLan;
        var p2p = _linkManager.WifiDirect;
        var bluetooth = _linkManager.Bluetooth;
        var usb = _linkManager.Usb;

        _linkManager.StateManager.OnStateChanged += state =>
        {
            ConnectionState = state;
            Notify();
        };
        _linkManager.ActiveLinkChanged += link =>
        {
            ActiveLink = link;
            StatusMessage = StatusForActiveLink();
            Notify();
        };
        _linkManager.RouteChanged += route =>
        {
            CurrentRoute = route;
            Notify();
        };

        lan.OnStatusChanged += message => UpdateStatus("lan", message);
        p2p.OnP2pStatusChanged += message => UpdateStatus("wifi-direct", message);
        bluetooth.OnStatusChanged += message => UpdateStatus("bluetooth", message);
        usb.OnStatusChanged += message => UpdateStatus("usb", message);

        p2p.OnP2pProgressVisible += visible =>
        {
            IsP2pProgressVisible = visible;
            Notify();
        };
        p2p.OnP2pProgress += (indeterminate, value) =>
        {
            IsP2pProgressIndeterminate = indeterminate;
            P2pProgress = value;
            Notify();
        };
        p2p.OnQrChanged += (payload, deviceName) => QrPayloadChanged?.Invoke(payload, deviceName);
    }

    public async Task InitializeAsync()
    {
        var result = await _initialization.RunAsync(new (string, Func<Task<bool>>)[] {
            ("LAN", _linkManager.StartLanAsync),
            ("蓝牙", _linkManager.StartBluetoothAsync),
            ("USB", _linkManager.StartUsbAsync),
        });
        StatusMessage = result.Message;
        LastError = result.Failed.Length > 0 ? result.Message : "";
        if (!_p2pStartingOrReady) StartP2pInBackground();
        Notify();
    }

    public async Task RefreshP2pAsync()
    {
        P2pStatus = "正在刷新 P2P 二维码...";
        Notify();
        await _linkManager.StopP2pAsync();
        StartP2pInBackground();
    }

    public async Task ConnectRecentP2pAsync()
    {
        P2pStatus = "正在重新等待已配对设备...";
        Notify();
        await _linkManager.StopP2pAsync();
        StartP2pInBackground();
    }

    public PairedDeviceStore.PairedInfo? GetRecentPair() => PairedDeviceStore.Load();

    private void UpdateStatus(string link, string message)
    {
        switch (link)
        {
            case "lan": LanStatus = message; break;
            case "wifi-direct": P2pStatus = message; break;
            case "bluetooth": BluetoothStatus = message; break;
            case "usb": UsbStatus = message; break;
        }

        if (link == ActiveLink || IsError(message))
            StatusMessage = message;

        if (IsError(message)) LastError = message;
        Notify();
    }

    private string StatusForActiveLink() => ActiveLink switch
    {
        "none" => "当前无活跃链路",
        "wifi-direct" => P2pStatus,
        "bluetooth" => BluetoothStatus,
        "usb" => UsbStatus,
        _ => LanStatus,
    };

    private static bool IsError(string message)
        => message.Contains("错误", StringComparison.OrdinalIgnoreCase)
           || message.Contains("失败", StringComparison.OrdinalIgnoreCase);

    private void Notify() => SnapshotChanged?.Invoke();

    private void StartP2pInBackground() { _p2pStartingOrReady = true; _ = RunP2pAsync(); }

    private async Task RunP2pAsync()
    {
        try
        {
            _p2pStartingOrReady = await _linkManager.StartP2pAsync();
        }
        catch (Exception ex)
        {
            _p2pStartingOrReady = false;
            LastError = $"P2P 启动失败：{ex.Message}";
            P2pStatus = LastError;
            Notify();
        }
    }

    public void Dispose() => _linkManager.Dispose();
}
