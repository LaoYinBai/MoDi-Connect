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
using System.IO;
using Avalonia.Media.Imaging;
using QRCoder;

namespace MoDi.Desktop;

/// <summary>
/// QrCodeHelper — QR 码生成工具
///
/// 使用 QRCoder 生成 PNG 字节流，转为 Avalonia Bitmap 供 Image 控件显示。
/// </summary>
public static class QrCodeHelper
{
    /// <summary>
    /// 生成 QR 码 Bitmap
    /// </summary>
    /// <param name="content">QR 码内容（如 MODI://...）</param>
    /// <param name="pixelsPerModule">每个模块的像素大小（默认 8）</param>
    public static Bitmap Generate(string content, int pixelsPerModule = 8)
    {
        var bytes = GeneratePng(content, pixelsPerModule);
        using var ms = new MemoryStream(bytes);
        return new Bitmap(ms);
    }

    /// <summary>生成可跨 UI 边界传递的 QR PNG 字节。</summary>
    public static byte[] GeneratePng(string content, int pixelsPerModule = 8)
    {
        using var gen = new QRCodeGenerator();
        var data = gen.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new BitmapByteQRCode(data).GetGraphic(pixelsPerModule);
    }

    /// <summary>
    /// 构建 MODI:// QR 码文本（WiFi Direct P2P 模式）
    /// </summary>
    /// <param name="deviceName">Windows 设备名</param>
    /// <param name="token">认证 token</param>
    public static string BuildQrPayload(string deviceName, string token)
    {
        return $"MODI://version=1&transport=wifidirect&device={deviceName}&token={token}";
    }
}
