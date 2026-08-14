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

namespace MoDi.Desktop.Links;

/// <summary>
/// 链路状态（双端统一）
/// </summary>
public enum LinkState
{
    Idle,        // 未启动
    Listening,   // 监听中（等待手机连接）
    Connected,   // 手机已连接（握手成功）
    Streaming,   // 音频传输中
}

/// <summary>
/// ILink — 统一链路接口（双端对齐）
///
/// 所有链路（WiFi LAN / WiFi Direct / Bluetooth / USB）实现此接口。
/// 接收端语义：ConnectAsync = 开始监听/发现，等待手机连接。
/// LinkManager 通过 linkType 分发，不关心具体实现。
/// </summary>
public interface ILink : IDisposable
{
    /// <summary>链路当前状态</summary>
    LinkState State { get; }

    /// <summary>链路是否活跃（State != Idle）</summary>
    bool IsActive { get; }

    /// <summary>启动链路（接收端：开始监听/发现，等待手机连接）</summary>
    Task<bool> ConnectAsync();

    /// <summary>停止链路（清理资源，停止监听，状态回到 Idle）</summary>
    Task DisconnectAsync();
}
