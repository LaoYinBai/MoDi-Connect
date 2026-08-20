package com.modi.connect.ui.runtime

import com.modi.connect.audio.StreamGain
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

internal class StreamVolumeController(
    private val scope: CoroutineScope,
    private val hideDelayMillis: Long = 1_200L,
) {
    private var hideJob: Job? = null

    fun adjust(
        streaming: Boolean,
        current: Float,
        delta: Float,
        onVolumeChanged: (Float) -> Unit,
        onHudVisibilityChanged: (Boolean) -> Unit,
    ): Boolean {
        if (!streaming) return false
        val normalized = StreamGain(current).adjust(delta)
        onVolumeChanged(normalized)
        onHudVisibilityChanged(true)
        hideJob?.cancel()
        hideJob = scope.launch {
            delay(hideDelayMillis)
            onHudVisibilityChanged(false)
        }
        return true
    }

    fun cancel() {
        hideJob?.cancel()
        hideJob = null
    }
}
