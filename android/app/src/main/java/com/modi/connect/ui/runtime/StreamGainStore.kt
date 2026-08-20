package com.modi.connect.ui.runtime

import android.content.Context
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

class StreamGainStore(
    context: Context,
    private val scope: CoroutineScope,
) {
    private val preferences = context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)

    fun read(): Float = normalize(preferences.getFloat(KEY, 1f))

    fun persist(value: Float) {
        val normalized = normalize(value)
        scope.launch(Dispatchers.IO) {
            preferences.edit().putFloat(KEY, normalized).apply()
        }
    }

    companion object {
        const val PREFERENCES = "modi_ui"
        const val KEY = "stream_volume_v1"

        internal fun normalize(value: Float): Float =
            if (value.isFinite()) value.coerceIn(0f, 1f) else 1f
    }
}
