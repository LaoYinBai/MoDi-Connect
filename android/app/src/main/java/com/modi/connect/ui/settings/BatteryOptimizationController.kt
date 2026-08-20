package com.modi.connect.ui.settings

import android.content.ActivityNotFoundException
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.PowerManager
import android.provider.Settings
import com.modi.connect.core.infrastructure.Log

class BatteryOptimizationController(private val context: Context) {
    private val preferences = context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)

    val isIgnoringBatteryOptimizations: Boolean
        get() = (context.getSystemService(Context.POWER_SERVICE) as PowerManager)
            .isIgnoringBatteryOptimizations(context.packageName)

    fun requestOnFirstStreamingAttempt(): Boolean {
        if (preferences.getBoolean(KEY_FIRST_STREAM_GUIDE_SHOWN, false)) return false
        preferences.edit().putBoolean(KEY_FIRST_STREAM_GUIDE_SHOWN, true).apply()
        if (isIgnoringBatteryOptimizations) return false
        val request = Intent(
            Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS,
            Uri.parse("package:${context.packageName}"),
        )
        return launchIfResolvable(request) || openOemSettings()
    }

    fun openOemSettings(): Boolean {
        for (spec in OemKeepAliveGuide.resolve(Build.MANUFACTURER)) {
            val intent = when (spec) {
                is KeepAliveIntentSpec.Component -> Intent().setComponent(ComponentName(spec.packageName, spec.className))
                KeepAliveIntentSpec.ApplicationDetails -> Intent(
                    Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                    Uri.parse("package:${context.packageName}"),
                )
            }.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            if (launchIfResolvable(intent)) return true
        }
        return false
    }

    private fun launchIfResolvable(intent: Intent): Boolean {
        if (intent.resolveActivity(context.packageManager) == null) return false
        return try {
            context.startActivity(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
            true
        } catch (e: ActivityNotFoundException) {
            Log.w(TAG, "KEEP_ALIVE_SETTINGS_UNAVAILABLE: ${intent.component ?: intent.action}")
            false
        } catch (e: SecurityException) {
            Log.w(TAG, "KEEP_ALIVE_SETTINGS_DENIED: ${intent.component ?: intent.action}")
            false
        }
    }

    companion object {
        private const val TAG = "BatteryOptimization"
        private const val PREFERENCES = "modi_keep_alive_v1"
        private const val KEY_FIRST_STREAM_GUIDE_SHOWN = "first_stream_guide_shown"
    }
}
