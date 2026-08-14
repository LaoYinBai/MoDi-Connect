package com.modi.connect.audio

import kotlin.math.sqrt

object AudioLevelMeter {
    fun fromPcm16Le(pcm: ByteArray): Float {
        val sampleCount = pcm.size / 2
        if (sampleCount == 0) return 0f

        var sumSquares = 0.0
        var index = 0
        repeat(sampleCount) {
            val low = pcm[index].toInt() and 0xff
            val high = pcm[index + 1].toInt()
            val sample = ((high shl 8) or low).toShort().toInt()
            val normalized = sample / 32767.0
            sumSquares += normalized * normalized
            index += 2
        }
        return sqrt(sumSquares / sampleCount).toFloat().coerceIn(0f, 1f)
    }
}
