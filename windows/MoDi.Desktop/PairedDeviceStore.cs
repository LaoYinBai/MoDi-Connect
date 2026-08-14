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
using System.IO;
using System.Text.Json;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop;

/// <summary>
/// 配对设备持久化 — 保存已配对的 P2P 设备信息
///
/// 存储位置：%APPDATA%/MoDi/paired.json
/// 核心设计：token 持久化（跨会话不变），实现免扫码重连。
/// 首次配对：生成 token → QR 展示 → Android 扫码保存
/// 后续连接：使用同一 token → Android 用相同 deviceName 建组 → Windows 按 token 过滤发现
/// </summary>
public static class PairedDeviceStore
{
    private static readonly string StoreDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MoDi");
    private static readonly string StorePath = Path.Combine(StoreDir, "paired.json");

    public sealed class PairedInfo
    {
        /// <summary>持久化 token（跨会话不变，用于设备匹配和认证）</summary>
        public string Token { get; set; } = "";
        /// <summary>Windows 设备名（用于 QR 展示）</summary>
        public string DeviceName { get; set; } = "";
        /// <summary>最后连接时间</summary>
        public DateTime LastConnected { get; set; }
        /// <summary>Android 设备名（配对成功后记录）</summary>
        public string? PeerDeviceName { get; set; }
        /// <summary>Windows DeviceInformation 发现到的 P2P 设备 ID（跨启动免扫码直连）</summary>
        public string? P2pDeviceId { get; set; }
    }

    /// <summary>加载已保存的配对信息（不存在则返回 null）</summary>
    public static PairedInfo? Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return null;
            var json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<PairedInfo>(json);
        }
        catch (Exception ex)
        {
            Log.W("PairedDeviceStore", $"Load failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>保存配对信息</summary>
    public static void Save(PairedInfo info)
    {
        try
        {
            Directory.CreateDirectory(StoreDir);
            var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StorePath, json);
            Log.I("PairedDeviceStore", $"Saved: token={info.Token}, device={info.DeviceName}");
        }
        catch (Exception ex)
        {
            Log.E("PairedDeviceStore", $"Save failed: {ex.Message}");
        }
    }

    /// <summary>获取或创建持久化 token（首次运行生成，后续复用）</summary>
    public static PairedInfo GetOrCreate()
    {
        var existing = Load();
        if (existing != null && !string.IsNullOrEmpty(existing.Token))
            return existing;

        var info = new PairedInfo
        {
            Token = GenerateToken(),
            DeviceName = Environment.MachineName,
            LastConnected = DateTime.MinValue,
        };
        Save(info);
        return info;
    }

    /// <summary>生成 8 字符 hex token</summary>
    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
