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
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.media.AudioAttributes
import android.media.AudioFocusRequest
import android.media.AudioManager
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import androidx.core.app.NotificationCompat
import com.modi.connect.audio.AndroidMuteRecovery

/**
 * 前台服务 — 推流期间保持后台采集能力
 *
 * - Android 14+: 系统音频采集需要 FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION
 * - Android 9+:  后台麦克风采集需要 FOREGROUND_SERVICE_TYPE_MICROPHONE
 *
 * 由 [MainActivity] 在 Start/Stop 推流时启停，不自主管理生命周期
 */
class StreamingService : Service() {

    companion object {
        const val CHANNEL_ID = "streaming_channel"
        const val NOTIFICATION_ID = 1002
    }

    private var wakeLock: PowerManager.WakeLock? = null
    private var audioFocusRequest: AudioFocusRequest? = null
    private var audioManager: AudioManager? = null

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
        acquireWakeLock()
        requestAudioFocus()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val notification = buildNotification()
        // Android 14+ 必须显式传类型：推流既用麦克风也走媒体投影（屏幕音频采集），
        // 三参显式声明两个类型，消除两参重载的推断歧义（此前两参导致 FGS 类型校验失败闪退）。
        try {
            startForeground(
                NOTIFICATION_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE or
                    ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION
            )
        } catch (e: SecurityException) {
            // OEM/受限场景拒绝 FGS 时优雅降级：采集已在推流中，仅失去后台保活，不崩应用。
        }
        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        AndroidMuteRecovery.restoreAndClear(this)
        releaseWakeLock()
        abandonAudioFocus()
        super.onDestroy()
    }

    // ── Partial WakeLock：防止 CPU 休眠导致采集线程停摆 ──
    private fun acquireWakeLock() {
        val pm = getSystemService(Context.POWER_SERVICE) as PowerManager
        wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "MoDi::Stream").apply {
            setReferenceCounted(false)
            acquire(2 * 60 * 60 * 1000L)  // 2h 上限防泄漏
        }
    }

    private fun releaseWakeLock() {
        wakeLock?.let { if (it.isHeld) it.release() }
        wakeLock = null
    }

    // ── AudioFocus：向系统声明音频优先级，降低被抢占概率 ──
    private fun requestAudioFocus() {
        audioManager = getSystemService(Context.AUDIO_SERVICE) as AudioManager
        audioFocusRequest = AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN)
            .setAudioAttributes(AudioAttributes.Builder()
                .setUsage(AudioAttributes.USAGE_MEDIA)
                .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                .build())
            .setOnAudioFocusChangeListener { /* 不响应焦点丢失，保持采集 */ }
            .build()
        audioManager?.requestAudioFocus(audioFocusRequest!!)
    }

    private fun abandonAudioFocus() {
        audioFocusRequest?.let { audioManager?.abandonAudioFocusRequest(it) }
        audioFocusRequest = null
        audioManager = null
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID, "音频推流",
                NotificationManager.IMPORTANCE_LOW
            ).apply { description = "推流期间保持后台运行" }
            val manager = getSystemService(NotificationManager::class.java)
            manager.createNotificationChannel(channel)
        }
    }

    private fun buildNotification(): Notification {
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("墨堤互联")
            .setContentText("正在推流中...")
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setOngoing(true)
            .build()
    }
}
