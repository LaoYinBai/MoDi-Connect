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
package com.modi.connect.core.enums

/**
 * 网络质量等级枚举
 */
enum class NetworkQuality {
    /** 未知 */
    Unknown,

    /** 优秀（局域网低延迟） */
    Excellent,

    /** 良好（WiFi 已连接且互联网可达） */
    Good,

    /** 较差（仅蜂窝数据或信号弱） */
    Poor,

    /** 已断开 */
    Disconnected
}
