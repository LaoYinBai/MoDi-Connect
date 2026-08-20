package com.modi.connect.audio

import android.content.Context
import android.media.AudioManager
import com.modi.connect.core.infrastructure.Log

class SharedPreferencesMuteRecoveryStore(context: Context) : MuteLedgerStorage {
    private val preferences = context.applicationContext.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)

    override fun read(): MuteLedger? {
        if (!preferences.getBoolean(KEY_ACTIVE, false)) return null
        return MuteLedger(
            active = true,
            originalVolume = preferences.getInt(KEY_ORIGINAL_VOLUME, 0),
            mutedAtEpochMillis = preferences.getLong(KEY_MUTED_AT, 0L),
        )
    }

    override fun write(ledger: MuteLedger): Boolean = preferences.edit()
        .putBoolean(KEY_ACTIVE, ledger.active)
        .putInt(KEY_ORIGINAL_VOLUME, ledger.originalVolume)
        .putLong(KEY_MUTED_AT, ledger.mutedAtEpochMillis)
        .commit()

    override fun clear(): Boolean = preferences.edit().clear().commit()

    companion object {
        const val PREFERENCES = "modi_mute_recovery_v1"
        private const val KEY_ACTIVE = "active"
        private const val KEY_ORIGINAL_VOLUME = "original_volume"
        private const val KEY_MUTED_AT = "muted_at_epoch_millis"
    }
}

internal class AudioManagerVolumeController(private val manager: AudioManager) : MediaVolumeController {
    override val current: Int get() = manager.getStreamVolume(AudioManager.STREAM_MUSIC)
    override val maximum: Int get() = manager.getStreamMaxVolume(AudioManager.STREAM_MUSIC)
    override fun set(value: Int) = manager.setStreamVolume(AudioManager.STREAM_MUSIC, value, 0)
}

object AndroidMuteRecovery {
    private const val TAG = "MuteRecovery"

    fun reconcileOnColdStart(context: Context): MuteReconcileResult {
        val manager = context.getSystemService(Context.AUDIO_SERVICE) as AudioManager
        val result = MuteRecoveryStore(SharedPreferencesMuteRecoveryStore(context))
            .reconcileOnColdStart(AudioManagerVolumeController(manager), System.currentTimeMillis())
        if (result != MuteReconcileResult.NONE) Log.i(TAG, "mute recovery result=$result")
        return result
    }

    fun restoreAndClear(context: Context): Boolean {
        val manager = context.getSystemService(Context.AUDIO_SERVICE) as AudioManager
        return MuteRecoveryStore(SharedPreferencesMuteRecoveryStore(context))
            .restoreAndClear(AudioManagerVolumeController(manager))
    }
}
