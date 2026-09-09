package com.modi.connect.ui.runtime

import android.content.Context
import android.content.Intent
import com.modi.connect.MediaProjectionService
import com.modi.connect.ui.settings.BatteryOptimizationController
import com.modi.connect.ui.settings.StreamingIntentStore

class ForegroundExecutionController(private val context: Context) {
    private val battery = BatteryOptimizationController(context)
    val unexpectedServiceLoss: Boolean = StreamingIntentStore.consumeUnexpectedLoss(context)
    val batteryOptimizationIgnored: Boolean get() = battery.isIgnoringBatteryOptimizations
    fun requestOnFirstStreamingAttempt() = battery.requestOnFirstStreamingAttempt()
    fun openKeepAliveSettings(): Boolean = battery.openOemSettings()
    fun clearStreamingIntent() = StreamingIntentStore.clear(context)
    fun stopProjectionPreparation() = context.stopService(Intent(context, MediaProjectionService::class.java))
}
