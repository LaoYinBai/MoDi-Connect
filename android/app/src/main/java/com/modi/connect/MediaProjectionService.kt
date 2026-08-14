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

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat

/**
 * 前台服务 — 用于满足 MediaProjection 前台服务类型要求
 *
 * Android 14+ 要求先启动带 FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION 的前台服务，
 * 才能获取 MediaProjection 授权。授权完成后即可停止，
 * 实际推流中的前台由 [StreamingService] 维持。
 */
class MediaProjectionService : Service() {

    companion object {
        const val CHANNEL_ID = "media_projection_channel"
        const val NOTIFICATION_ID = 1001
    }

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val notification = buildNotification()
        // Android 14+ 必须显式传类型；三参消除两参重载在多变体声明下的推断歧义。
        // 授权桥只做媒体投影授权，固定 mediaProjection 类型。
        try {
            startForeground(
                NOTIFICATION_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION
            )
        } catch (e: SecurityException) {
            // 部分 OEM/受限场景可能拒绝 FGS——授权桥失败时优雅降级，不崩应用。
            stopSelf()
            return START_NOT_STICKY
        }
        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    // ── 通知渠道（Android 8+ 必须） ──
    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID, "音频桥接",
                NotificationManager.IMPORTANCE_LOW
            ).apply { description = "用于 MediaProjection 系统音频采集" }
            val manager = getSystemService(NotificationManager::class.java)
            manager.createNotificationChannel(channel)
        }
    }

    private fun buildNotification(): Notification {
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("墨堤互联")
            .setContentText("准备系统音频采集...")
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setOngoing(false)
            .build()
    }
}
