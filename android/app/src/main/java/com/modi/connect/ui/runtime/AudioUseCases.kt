package com.modi.connect.ui.runtime

import android.content.Context
import android.media.projection.MediaProjection
import com.modi.connect.audio.AndroidMuteRecovery
import com.modi.connect.audio.AudioPipeline
import com.modi.connect.audio.MediaProjectionOwner
import com.modi.connect.audio.SharedPreferencesMuteRecoveryStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

class AudioUseCases(
    private val context: Context,
    scope: CoroutineScope,
    onProjectionEnded: () -> Unit,
) {
    private val gainStore = StreamGainStore(context, scope)
    private val projectionOwner = MediaProjectionOwner(scope) { onProjectionEnded() }
    val pipeline = AudioPipeline().also { it.setStreamVolume(gainStore.read()) }
    val muteRecoveryJob: Job = scope.launch(Dispatchers.IO) { AndroidMuteRecovery.reconcileOnColdStart(context) }
    val hasProjection: Boolean get() = projectionOwner.hasProjection
    val muteRecoveryPending: Boolean get() = SharedPreferencesMuteRecoveryStore(context).read()?.active == true
    fun projection(): MediaProjection? = projectionOwner.current()
    fun replaceProjection(projection: MediaProjection?) = projectionOwner.replace(projection)
    fun clearProjection(stopProjection: Boolean) = projectionOwner.clear(stopProjection)
    fun setVolume(value: Float): Float = pipeline.setStreamVolume(value).also(gainStore::persist)
}
