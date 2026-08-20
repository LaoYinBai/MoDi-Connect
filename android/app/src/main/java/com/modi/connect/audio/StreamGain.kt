package com.modi.connect.audio

import java.util.concurrent.atomic.AtomicInteger

class StreamGain(initialValue: Float = 1f) {
    private val bits = AtomicInteger(clamp(initialValue).toBits())

    val value: Float get() = Float.fromBits(bits.get())

    fun set(value: Float): Float {
        val normalized = clamp(value)
        bits.set(normalized.toBits())
        return normalized
    }

    fun adjust(delta: Float): Float = set(value + delta)

    fun applyTo(target: (Float) -> Unit) = target(value)

    companion object {
        const val HARDWARE_KEY_STEP = 1f / 15f
        private fun clamp(value: Float) = if (value.isFinite()) value.coerceIn(0f, 1f) else 1f
    }
}
