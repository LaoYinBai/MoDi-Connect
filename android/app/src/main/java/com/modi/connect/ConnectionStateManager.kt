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
package com.modi.connect

/**
 * 连接状态枚举 — 用户可见的连接过程
 *
 * 与 AudioPipeline.streaming 是两套独立维度：
 * streaming 是内部生命周期标志，这里是 UI 观测层
 * 两套并存，互不取代
 *
 * ## 迁移规则
 *   IDLE ──→ SEARCHING ──→ FOUND ──→ CONNECTING ──→ CONNECTED ──→ STREAMING
 *     ↑         ↑                                            ↓            ↓
 *     │         └────────────────────────────────── RECONNECTING ─────────┘
 *     │                                                       ↓
 *     └──────────────── DISCONNECTED ←──────────────────────┘
 *
 *   ERROR ← 任意状态（除 DISCONNECTED 外）
 *   DISCONNECTED ← 任意状态
 */
enum class ConnectionState(val code: Int) {
    IDLE(0),           // 初始就绪，等待用户操作
    DISCONNECTED(1),   // 连接中断（物理断开 / 用户终止）
    SEARCHING(2),      // 正在扫描局域网设备
    FOUND(3),          // 发现电脑
    CONNECTING(4),     // 正在握手连接
    CONNECTED(5),      // 握手成功
    STREAMING(6),      // 音频正在传输中
    RECONNECTING(7),   // 连接恢复中
    ERROR(8);          // 确定性错误（端口绑定失败等）

    /** 中文 UI 标签映射 */
    fun toChineseLabel(): String = when (this) {
        IDLE -> "就绪"
        DISCONNECTED -> "未连接"
        SEARCHING -> "正在寻找电脑"
        FOUND -> "已发现设备"
        CONNECTING -> "正在连接"
        CONNECTED -> "已连接"
        STREAMING -> "音频传输中"
        RECONNECTING -> "连接恢复中"
        ERROR -> "连接失败"
    }
}

/**
 * 连接状态管理器 — 旁路观测层
 *
 * 职责：
 * - 维护当前连接状态
 * - 校验状态转换的合法性（禁止转换直接静默忽略）
 * - 状态变化时通过回调通知上层（UI）
 * - 记录状态变化的 reason（用于 UI 显示错误原因）
 *
 * 不替代任何现有生命周期标志（streaming / _running）
 * 对齐 P3 统一状态机规则
 */
class ConnectionStateManager {

    @Volatile
    var state = ConnectionState.IDLE
        private set

    /** 上次状态变化的 reason（用于 UI 显示错误/恢复原因） */
    var lastReason: String? = null
        private set

    /** 状态变更回调，MainActivity 绑定到这个回调来更新 UI */
    var onStateChanged: ((ConnectionState) -> Unit)? = null

    /**
     * 更新状态。校验合法转换，相同状态不触发回调。
     */
    fun update(newState: ConnectionState, reason: String? = null) {
        if (state == newState) return
        if (!isValidTransition(newState)) return

        state = newState
        if (reason != null) lastReason = reason
        onStateChanged?.invoke(state)
    }

    /** 清空 reason（当用户重新开始连接时调用） */
    fun clearLastReason() { lastReason = null }

    /** 将任意可重新开始的状态规范化推进到 CONNECTING，不放宽状态转换表。 */
    fun beginConnecting() {
        if (state == ConnectionState.ERROR) update(ConnectionState.DISCONNECTED)
        if (state == ConnectionState.IDLE || state == ConnectionState.DISCONNECTED) {
            update(ConnectionState.SEARCHING)
        }
        if (state == ConnectionState.SEARCHING) update(ConnectionState.FOUND)
        if (state == ConnectionState.FOUND || state == ConnectionState.RECONNECTING) {
            update(ConnectionState.CONNECTING)
        }
    }

    /**
     * 检查 from → to 转换合法性（对齐 P3 统一状态机）
     */
    private fun isValidTransition(to: ConnectionState): Boolean {
        return when (state) {
            ConnectionState.IDLE -> to == ConnectionState.SEARCHING || to == ConnectionState.DISCONNECTED || to == ConnectionState.ERROR
            ConnectionState.DISCONNECTED -> to == ConnectionState.SEARCHING || to == ConnectionState.IDLE || to == ConnectionState.ERROR
            ConnectionState.SEARCHING -> to == ConnectionState.FOUND || to == ConnectionState.DISCONNECTED || to == ConnectionState.ERROR
            ConnectionState.FOUND -> to == ConnectionState.CONNECTING || to == ConnectionState.DISCONNECTED || to == ConnectionState.ERROR
            ConnectionState.CONNECTING -> to == ConnectionState.CONNECTED || to == ConnectionState.ERROR || to == ConnectionState.DISCONNECTED
            ConnectionState.CONNECTED -> to == ConnectionState.STREAMING || to == ConnectionState.DISCONNECTED || to == ConnectionState.ERROR
            ConnectionState.STREAMING -> to == ConnectionState.RECONNECTING || to == ConnectionState.DISCONNECTED || to == ConnectionState.ERROR
            ConnectionState.RECONNECTING -> to == ConnectionState.CONNECTING || to == ConnectionState.CONNECTED || to == ConnectionState.SEARCHING || to == ConnectionState.DISCONNECTED || to == ConnectionState.ERROR
            ConnectionState.ERROR -> to == ConnectionState.DISCONNECTED || to == ConnectionState.IDLE
        }
    }
}
