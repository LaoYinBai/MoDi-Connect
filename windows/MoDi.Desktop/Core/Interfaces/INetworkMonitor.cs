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
using MoDi.Protocol;

namespace MoDi.Core;

/// <summary>
/// INetworkMonitor — 统一网络状态监听接口
///
/// 职责仅限"网络状态监听"，不含重连逻辑。
/// 重连逻辑由 ReconnectionManager 基于此接口的事件触发。
///
/// 当前实现：
///   Windows — WinNetworkMonitor（基于 System.Net.NetworkInformation.NetworkChange）
///   Android — AndroidNetworkMonitor（基于 ConnectivityManager.NetworkCallback）
/// </summary>
public interface INetworkMonitor
{
    /// <summary>开始监听网络状态变化</summary>
    void Start();

    /// <summary>停止监听</summary>
    void Stop();

    /// <summary>当前是否有网络连接</summary>
    bool IsConnected { get; }

    /// <summary>当前活跃的传输类型</summary>
    TransportType ActiveTransport { get; }

    /// <summary>当前网络质量</summary>
    NetworkQuality Quality { get; }

    /// <summary>网络状态变化时触发</summary>
    event Action<NetworkInfo>? OnNetworkChanged;
}
