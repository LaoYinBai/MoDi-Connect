package com.modi.connect.audio

import android.media.projection.MediaProjection
import android.os.Handler
import android.os.Looper
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.launch

internal interface ProjectionHandle {
    fun register(onStopped: () -> Unit)
    fun unregister()
    fun stop()
    fun projection(): MediaProjection?
}

class MediaProjectionOwner internal constructor(
    private val scope: CoroutineScope,
    private val onStopped: suspend () -> Unit,
) {
    private val lock = Any()
    private var currentHandle: ProjectionHandle? = null

    val hasProjection: Boolean get() = synchronized(lock) { currentHandle != null }

    fun current(): MediaProjection? = synchronized(lock) { currentHandle?.projection() }

    fun replace(projection: MediaProjection?) {
        if (projection == null) clear(stopProjection = true)
        else replace(AndroidProjectionHandle(projection))
    }

    internal fun replace(next: ProjectionHandle) {
        next.register { handleSystemStop(next) }
        val previous = synchronized(lock) {
            val old = currentHandle
            currentHandle = next
            old
        }
        if (previous !== next) {
            previous?.unregister()
            previous?.stop()
        }
    }

    fun clear(stopProjection: Boolean) {
        val previous = synchronized(lock) {
            val old = currentHandle
            currentHandle = null
            old
        } ?: return
        previous.unregister()
        if (stopProjection) previous.stop()
    }

    private fun handleSystemStop(stopped: ProjectionHandle) {
        val owned = synchronized(lock) {
            if (currentHandle !== stopped) false
            else {
                currentHandle = null
                true
            }
        }
        if (!owned) return
        stopped.unregister()
        scope.launch { onStopped() }
    }

    private class AndroidProjectionHandle(
        private val mediaProjection: MediaProjection,
    ) : ProjectionHandle {
        private var callback: MediaProjection.Callback? = null

        override fun register(onStopped: () -> Unit) {
            val registered = object : MediaProjection.Callback() {
                override fun onStop() = onStopped()
            }
            mediaProjection.registerCallback(registered, Handler(Looper.getMainLooper()))
            callback = registered
        }

        override fun unregister() {
            callback?.let(mediaProjection::unregisterCallback)
            callback = null
        }

        override fun stop() = mediaProjection.stop()
        override fun projection(): MediaProjection = mediaProjection
    }
}
