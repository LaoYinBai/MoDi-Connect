package com.modi.connect.audio

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class MuteRecoveryStoreTest {
    @Test
    fun ledger_is_durable_before_muting_and_normal_stop_restores() {
        val events = mutableListOf<String>()
        val storage = FakeStorage(events)
        val volume = FakeVolume(current = 9, maximum = 15, events = events)
        val store = MuteRecoveryStore(storage)

        assertTrue(store.recordBeforeMute(volume, 1_000L))
        assertEquals(listOf("write:9", "volume:0"), events)
        assertTrue(store.restoreAndClear(volume))
        assertEquals(9, volume.current)
        assertNull(storage.ledger)
    }

    @Test
    fun failed_mute_clears_the_ledger() {
        val storage = FakeStorage()
        val volume = FakeVolume(current = 7, maximum = 15, failOnZero = true)

        assertFalse(MuteRecoveryStore(storage).recordBeforeMute(volume, 1_000L))
        assertNull(storage.ledger)
    }

    @Test
    fun cold_start_restores_active_recent_ledger_and_clamps_to_current_maximum() {
        val storage = FakeStorage().apply { ledger = MuteLedger(true, 20, 1_000L) }
        val volume = FakeVolume(current = 0, maximum = 12)

        assertEquals(MuteReconcileResult.RESTORED, MuteRecoveryStore(storage).reconcileOnColdStart(volume, 2_000L))
        assertEquals(12, volume.current)
        assertNull(storage.ledger)
    }

    @Test
    fun stale_ledger_is_cleared_without_changing_volume() {
        val storage = FakeStorage().apply { ledger = MuteLedger(true, 8, 1_000L) }
        val volume = FakeVolume(current = 3, maximum = 15)

        val now = 1_000L + MuteRecoveryStore.MAX_LEDGER_AGE_MILLIS + 1
        assertEquals(MuteReconcileResult.STALE_CLEARED, MuteRecoveryStore(storage).reconcileOnColdStart(volume, now))
        assertEquals(3, volume.current)
        assertNull(storage.ledger)
    }

    private class FakeStorage(private val events: MutableList<String> = mutableListOf()) : MuteLedgerStorage {
        var ledger: MuteLedger? = null
        override fun read(): MuteLedger? = ledger
        override fun write(ledger: MuteLedger): Boolean {
            events += "write:${ledger.originalVolume}"
            this.ledger = ledger
            return true
        }
        override fun clear(): Boolean { ledger = null; return true }
    }

    private class FakeVolume(
        override var current: Int,
        override val maximum: Int,
        private val events: MutableList<String> = mutableListOf(),
        private val failOnZero: Boolean = false,
    ) : MediaVolumeController {
        override fun set(value: Int) {
            events += "volume:$value"
            if (value == 0 && failOnZero) error("mute failed")
            current = value
        }
    }
}
