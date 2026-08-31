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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Threading.Tasks;
using MoDi.Desktop.Core.Session;
using MoDi.Protocol;

namespace MoDi.Desktop.Links;

/// <summary>
/// LinkManager — 纯路由器 + 活跃链路管理
///
/// 职责：
/// 1. 持有各链路实例，转发操作
/// 2. 跟踪当前活跃链路（ActiveLinkType）
/// 3. Volume/Route 操作当前活跃引擎（而非固定 WifiLan）
/// 4. BT/USB 会话开始时暂停 LAN 引擎，结束时恢复
/// </summary>
public sealed class LinkManager : IDisposable
{
    // ── 四级链路实例 ──
    private readonly WifiLanLink _wifiLan;
    private readonly WifiDirectLink _wifiDirect;
    private readonly BluetoothLink _bluetooth;
    private readonly UsbLink _usb;
    private readonly ConnectionStateManager _stateManager = new();
    private readonly SessionSwitchCoordinator _sessions = new();
    private readonly bool _managePhysicalLinks;

    // ── 活跃链路跟踪 ──
    private string _activeLink = "none";

    public event Action<string>? ActiveLinkChanged;
    public event Action<int>? RouteChanged;

    // ── 公开属性 ──
    public WifiLanLink WifiLan => _wifiLan;
    public WifiDirectLink WifiDirect => _wifiDirect;
    public BluetoothLink Bluetooth => _bluetooth;
    public UsbLink Usb => _usb;
    public ConnectionStateManager StateManager => _stateManager;
    internal ActiveSession? CurrentSession => _sessions.Current;

    /// <summary>当前活跃链路类型（UI 显示用）</summary>
    public string ActiveLinkType => _activeLink;

    /// <summary>音量操作当前活跃引擎（LAN/BT/USB 推流时均有效）</summary>
    public float Volume
    {
        get => GetActiveEngine()?.Volume ?? 1.0f;
        set
        {
            var engine = GetActiveEngine();
            if (engine != null) engine.Volume = value;
        }
    }

    public LinkManager() : this(managePhysicalLinks: true) { }

    internal LinkManager(bool managePhysicalLinks)
    {
        _managePhysicalLinks = managePhysicalLinks;
        _wifiLan = managePhysicalLinks
            ? new WifiLanLink(_stateManager)
            : new WifiLanLink(_stateManager, audioPort: 0, handshakePort: 0);
        _wifiDirect = new WifiDirectLink(_stateManager, HandleRoute);
        _bluetooth = new BluetoothLink(_stateManager);
        _usb = new UsbLink(_stateManager);

        _wifiLan.OnSessionStarted += sessionId => AcceptSession(LinkType.WifiLan, sessionId);
        _bluetooth.OnSessionStarted += sessionId => AcceptSession(LinkType.Bluetooth, sessionId);
        _bluetooth.OnSessionEnded += sessionId => EndSession(LinkType.Bluetooth, sessionId);
        _usb.OnSessionStarted += sessionId => AcceptSession(LinkType.Usb, sessionId);
        _usb.OnSessionEnded += sessionId => EndSession(LinkType.Usb, sessionId);
        _wifiDirect.OnSessionStarted += sessionId => AcceptSession(LinkType.WifiDirect, sessionId);
        _wifiDirect.OnSessionEnded += sessionId => EndSession(LinkType.WifiDirect, sessionId);

        _wifiLan.Handshake.OnDisconnectRequest = AcceptDisconnect;
        _bluetooth.OnDisconnectRequest = AcceptDisconnect;
        _usb.OnDisconnectRequest = AcceptDisconnect;

        _wifiLan.OnRouteChanged += route =>
        {
            if (_activeLink is not "bluetooth" and not "usb" and not "wifi-direct")
                SetActiveLink("lan");
            RouteChanged?.Invoke(route);
        };
        _bluetooth.OnRouteChanged += route => RouteChanged?.Invoke(route);
        _usb.OnRouteChanged += route => RouteChanged?.Invoke(route);
    }

    // ── 会话协调 ──

    internal ActiveSession AcceptSession(byte linkType, Guid sessionId)
    {
        var previous = _sessions.Current;
        if (previous is { } old && (old.LinkType != linkType || old.SessionId != sessionId))
            StopPhysicalSession(old.LinkType, closeTransport: true);

        var active = _sessions.Activate(linkType, sessionId);
        if (_managePhysicalLinks)
        {
            if (linkType is LinkType.WifiLan or LinkType.WifiDirect)
                _wifiLan.ResumeEngine();
            else
                _wifiLan.PauseEngine();
        }

        SetActiveLink(LinkName(linkType));
        _stateManager.BeginConnecting();
        return active;
    }

    internal SessionControlMessage AcceptDisconnect(
        SessionControlMessage request,
        byte receivedOnLink)
    {
        var decision = _sessions.HandleDisconnect(request, receivedOnLink);
        if (decision.Accepted && decision.Ended is { } ended)
        {
            StopPhysicalSession(ended.LinkType, closeTransport: false);
            SetActiveLink("none");
            _stateManager.Update(ConnectionState.Disconnected);
        }
        return decision.Ack;
    }

    internal bool EndSession(byte linkType, Guid sessionId)
    {
        if (!_sessions.EndIfCurrent(linkType, sessionId)) return false;
        StopPhysicalSession(linkType, closeTransport: false);
        SetActiveLink("none");
        _stateManager.Update(ConnectionState.Disconnected);
        return true;
    }

    private void StopPhysicalSession(byte linkType, bool closeTransport)
    {
        if (!_managePhysicalLinks) return;
        switch (linkType)
        {
            case LinkType.WifiLan:
            case LinkType.WifiDirect:
                _wifiLan.PauseEngine();
                break;
            case LinkType.Bluetooth:
                _bluetooth.StopCurrentSession(closeTransport);
                break;
            case LinkType.Usb:
                _usb.StopCurrentSession(closeTransport);
                break;
        }
    }

    private static string LinkName(byte linkType) => linkType switch
    {
        LinkType.WifiLan => "lan",
        LinkType.WifiDirect => "wifi-direct",
        LinkType.Bluetooth => "bluetooth",
        LinkType.Usb => "usb",
        _ => "none",
    };

    /// <summary>获取当前活跃的 AudioEngine</summary>
    private AudioEngine? GetActiveEngine()
    {
        return _activeLink switch
        {
            "bluetooth" => _bluetooth.ActiveEngine ?? _wifiLan.Engine,
            "usb" => _usb.ActiveEngine ?? _wifiLan.Engine,
            "lan" or "wifi-direct" => _wifiLan.Engine,
            _ => null,
        };
    }

    private void SetActiveLink(string linkType)
    {
        if (_activeLink == linkType) return;
        _activeLink = linkType;
        ActiveLinkChanged?.Invoke(linkType);
    }

    /// <summary>共享路由控制 — LAN/P2P 握手成功后通过这里设置 AudioRouter 模式</summary>
    private bool HandleRoute(int route) => _wifiLan.HandleRoute(route);

    // ── 操作转发 ──

    public Task<bool> StartLanAsync() => _wifiLan.ConnectAsync();
    public Task<bool> StartP2pAsync() => _wifiDirect.ConnectAsync();
    public Task StopP2pAsync() => _wifiDirect.DisconnectAsync();
    public Task<bool> StartBluetoothAsync() => _bluetooth.ConnectAsync();
    public Task StopBluetoothAsync() => _bluetooth.DisconnectAsync();
    public Task<bool> StartUsbAsync() => _usb.ConnectAsync();
    public Task StopUsbAsync() => _usb.DisconnectAsync();

    public bool IsP2pActive => _wifiDirect.IsActive;
    public bool IsBluetoothActive => _bluetooth.IsActive;
    public bool IsUsbActive => _usb.IsActive;

    public void Dispose()
    {
        _wifiLan.Dispose();
        _wifiDirect.Dispose();
        _bluetooth.Dispose();
        _usb.Dispose();
    }
}
