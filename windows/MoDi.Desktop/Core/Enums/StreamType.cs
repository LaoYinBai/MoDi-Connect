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
namespace MoDi.Core;

/// <summary>
/// 流类型枚举 — 标识数据通道中承载的流种类
///
/// 当前仅 Audio 在用，其余为预留扩展。
/// </summary>
public enum StreamType
{
    /// <summary>音频流（当前唯一在用）</summary>
    Audio,

    /// <summary>视频流（预留：投屏）</summary>
    Video,

    /// <summary>文件流（预留：文件传输）</summary>
    File,

    /// <summary>控制流（预留：剪切板/远程指令）</summary>
    Control
}
