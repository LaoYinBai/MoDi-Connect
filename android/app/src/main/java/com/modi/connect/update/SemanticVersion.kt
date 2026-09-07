package com.modi.connect.update

internal data class SemanticVersion(
    val major: Long,
    val minor: Long,
    val patch: Long,
    val prerelease: String?,
    val prereleaseNumber: Long?,
) : Comparable<SemanticVersion> {
    val isPrerelease: Boolean get() = prerelease != null
    val releaseIdentity: String get() = prerelease ?: "stable"

    override fun compareTo(other: SemanticVersion): Int {
        major.compareTo(other.major).takeIf { it != 0 }?.let { return it }
        minor.compareTo(other.minor).takeIf { it != 0 }?.let { return it }
        patch.compareTo(other.patch).takeIf { it != 0 }?.let { return it }
        if (!isPrerelease && !other.isPrerelease) return 0
        if (!isPrerelease) return 1
        if (!other.isPrerelease) return -1
        val rank = if (prerelease == "beta") 0 else 1
        val otherRank = if (other.prerelease == "beta") 0 else 1
        rank.compareTo(otherRank).takeIf { it != 0 }?.let { return it }
        return prereleaseNumber!!.compareTo(other.prereleaseNumber!!)
    }

    override fun toString(): String = if (isPrerelease)
        "$major.$minor.$patch-$prerelease.$prereleaseNumber" else "$major.$minor.$patch"

    companion object {
        private val pattern = Regex("^(?:v)?(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-(beta|rc)\\.(0|[1-9]\\d*))?$")

        fun parse(value: String): SemanticVersion {
            val match = pattern.matchEntire(value) ?: throw IllegalArgumentException("Invalid MoDi semantic version: $value")
            return SemanticVersion(
                match.groupValues[1].toLong(),
                match.groupValues[2].toLong(),
                match.groupValues[3].toLong(),
                match.groupValues[4].ifEmpty { null },
                match.groupValues[5].ifEmpty { null }?.toLong(),
            )
        }

        fun parseOrNull(value: String): SemanticVersion? = runCatching { parse(value) }.getOrNull()
    }
}

