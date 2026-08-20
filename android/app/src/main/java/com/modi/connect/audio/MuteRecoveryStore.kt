package com.modi.connect.audio

data class MuteLedger(
    val active: Boolean,
    val originalVolume: Int,
    val mutedAtEpochMillis: Long,
)

interface MuteLedgerStorage {
    fun read(): MuteLedger?
    fun write(ledger: MuteLedger): Boolean
    fun clear(): Boolean
}

interface MediaVolumeController {
    val current: Int
    val maximum: Int
    fun set(value: Int)
}

enum class MuteReconcileResult { NONE, RESTORED, STALE_CLEARED, FAILED }

class MuteRecoveryStore(private val storage: MuteLedgerStorage) {
    fun isActive(): Boolean = storage.read()?.active == true

    fun recordBeforeMute(volume: MediaVolumeController, nowEpochMillis: Long): Boolean {
        val ledger = MuteLedger(true, volume.current, nowEpochMillis)
        if (!storage.write(ledger)) return false
        return try {
            volume.set(0)
            true
        } catch (_: Exception) {
            storage.clear()
            false
        }
    }

    fun restoreAndClear(volume: MediaVolumeController): Boolean {
        val ledger = storage.read()?.takeIf { it.active } ?: return true
        return try {
            volume.set(ledger.originalVolume.coerceIn(0, volume.maximum.coerceAtLeast(0)))
            storage.clear()
        } catch (_: Exception) {
            false
        }
    }

    fun reconcileOnColdStart(volume: MediaVolumeController, nowEpochMillis: Long): MuteReconcileResult {
        val ledger = storage.read()?.takeIf { it.active } ?: return MuteReconcileResult.NONE
        if (nowEpochMillis - ledger.mutedAtEpochMillis > MAX_LEDGER_AGE_MILLIS) {
            return if (storage.clear()) MuteReconcileResult.STALE_CLEARED else MuteReconcileResult.FAILED
        }
        return if (restoreAndClear(volume)) MuteReconcileResult.RESTORED else MuteReconcileResult.FAILED
    }

    companion object {
        const val MAX_LEDGER_AGE_MILLIS = 24L * 60L * 60L * 1_000L
    }
}
