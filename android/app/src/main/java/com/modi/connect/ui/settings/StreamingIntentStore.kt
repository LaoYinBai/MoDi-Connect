package com.modi.connect.ui.settings

import android.content.Context

object StreamingIntentStore {
    private const val PREFERENCES = "modi_keep_alive_v1"
    private const val KEY_STREAMING_INTENDED = "streaming_intended"

    fun markActive(context: Context) {
        context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)
            .edit().putBoolean(KEY_STREAMING_INTENDED, true).apply()
    }

    fun clear(context: Context) {
        context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)
            .edit().remove(KEY_STREAMING_INTENDED).apply()
    }

    fun consumeUnexpectedLoss(context: Context): Boolean {
        val preferences = context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)
        val present = preferences.getBoolean(KEY_STREAMING_INTENDED, false)
        if (present) preferences.edit().remove(KEY_STREAMING_INTENDED).commit()
        return present
    }
}
