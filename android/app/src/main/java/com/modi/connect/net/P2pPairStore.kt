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
package com.modi.connect.net

import android.content.Context
import android.content.SharedPreferences
import com.modi.connect.core.infrastructure.Log

/**
 * P2pPairStore — P2P 配对设备持久化（SharedPreferences）
 *
 * 核心设计：token 持久化（跨会话不变），实现免扫码重连。
 * 首次配对：扫码获取 token → 保存
 * 后续连接：读取已保存 token → createGroup → Windows 自动发现并连接
 */
object P2pPairStore {

    private const val TAG = "P2pPairStore"
    private const val PREFS_NAME = "modi_paired"
    private const val KEY_TOKEN = "p2p_token"
    private const val KEY_DEVICE_NAME = "p2p_device_name"
    private const val KEY_LAST_CONNECTED = "p2p_last_connected"

    data class PairedInfo(
        val token: String,
        val deviceName: String,
        val lastConnected: Long
    )

    private fun prefs(context: Context): SharedPreferences =
        context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

    /** 加载已保存的配对信息（不存在则返回 null） */
    fun load(context: Context): PairedInfo? {
        val p = prefs(context)
        val token = p.getString(KEY_TOKEN, null) ?: return null
        val deviceName = p.getString(KEY_DEVICE_NAME, "") ?: ""
        val lastConnected = p.getLong(KEY_LAST_CONNECTED, 0L)
        return PairedInfo(token, deviceName, lastConnected)
    }

    /** 保存配对信息 */
    fun save(context: Context, token: String, deviceName: String) {
        prefs(context).edit()
            .putString(KEY_TOKEN, token)
            .putString(KEY_DEVICE_NAME, deviceName)
            .putLong(KEY_LAST_CONNECTED, System.currentTimeMillis())
            .apply()
        Log.i(TAG, "Paired device saved: token=${token.take(4)}..., device=$deviceName")
    }

    /** 是否有已配对设备 */
    fun hasPaired(context: Context): Boolean = load(context) != null

    /** 清除配对 */
    fun clear(context: Context) {
        prefs(context).edit().clear().apply()
        Log.i(TAG, "Paired device cleared")
    }
}
