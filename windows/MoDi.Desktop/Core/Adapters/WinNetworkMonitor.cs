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
using System.Net.NetworkInformation;
using MoDi.Core.Infrastructure;
using MoDi.Protocol;

namespace MoDi.Core.Adapters;

/// <summary>
/// WinNetworkMonitor — Windows 网络状态监听适配器
///
/// 基于 System.Net.NetworkInformation.NetworkChange 事件。
/// 职责仅限网络状态监听，不含重连逻辑。
/// </summary>
public sealed class WinNetworkMonitor : INetworkMonitor, IDisposable
{
    private const string Tag = "WinNetworkMonitor";
    private bool _started;
    private bool _disposed;

    public bool IsConnected { get; private set; }
    public TransportType ActiveTransport { get; private set; } = TransportType.Udp;
    public NetworkQuality Quality { get; private set; } = NetworkQuality.Unknown;

    public event Action<NetworkInfo>? OnNetworkChanged;

    public void Start()
    {
        if (_started) return;
        _started = true;

        NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnAddressChanged;

        // 初始状态评估
        EvaluateNetworkState();
        Log.I(Tag, $"Started: IsConnected={IsConnected}, Quality={Quality}");
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;

        NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnAddressChanged;
        Log.I(Tag, "Stopped");
    }

    private void OnAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        EvaluateNetworkState();
        NotifyChanged();
    }

    private void OnAddressChanged(object? sender, EventArgs e)
    {
        EvaluateNetworkState();
        NotifyChanged();
    }

    private void EvaluateNetworkState()
    {
        IsConnected = NetworkInterface.GetIsNetworkAvailable();

        if (!IsConnected)
        {
            Quality = NetworkQuality.Disconnected;
            return;
        }

        // 简单质量评估：有活跃的非回环 IPv4 接口即为 Good
        Quality = NetworkQuality.Good;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var props = ni.GetIPProperties();
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    // 有局域网 IPv4 地址 → 至少 Good
                    Quality = NetworkQuality.Good;
                    return;
                }
            }
        }
    }

    private void NotifyChanged()
    {
        var info = new NetworkInfo
        {
            IsConnected = IsConnected,
            TransportType = ActiveTransport,
            Quality = Quality,
            Ssid = null,
            InterfaceName = GetActiveInterfaceName(),
        };
        OnNetworkChanged?.Invoke(info);
    }

    private static string? GetActiveInterfaceName()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus == OperationalStatus.Up
                && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                return ni.Name;
        }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
