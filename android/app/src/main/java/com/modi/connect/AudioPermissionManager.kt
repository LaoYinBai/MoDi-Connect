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

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat

/**
 * AudioPermissionManager — 音频权限管理
 *
 * ## 职责
 * 1. 麦克风权限请求（RECORD_AUDIO）
 * 2. 系统音频授权（MediaProjection）
 *
 * 封装权限 launcher，与 MainActivity UI 逻辑解耦。
 */
class AudioPermissionManager(
    private val act: ComponentActivity
) {
    /** 麦克风权限是否已授予 */
    val micGranted: Boolean
        get() = ContextCompat.checkSelfPermission(
            act, Manifest.permission.RECORD_AUDIO
        ) == PackageManager.PERMISSION_GRANTED

    /** MediaProjection 实例 */
    var projection: MediaProjection? = null
        private set

    /** MediaProjection 是否已就绪 */
    var projectionReady: Boolean = false
        private set

    /** 发起麦克风权限请求（结果由外部回调处理） */
    fun launchMicPermission() {
        // 实际使用时通过 rememberLauncherForActivityResult 创建
    }

    /** 发起系统音频授权请求 */
    fun launchSystemAudio() {
        act.startForegroundService(Intent(act, MediaProjectionService::class.java))
        val mgr = act.getSystemService(MediaProjectionManager::class.java)
        // 实际需要通过 Intent 启动，此处标记
    }
}
