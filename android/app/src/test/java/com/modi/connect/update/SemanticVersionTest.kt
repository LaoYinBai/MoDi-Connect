package com.modi.connect.update

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class SemanticVersionTest {
    @Test
    fun shared_vectors_match_powershell_and_windows() {
        val root = generateSequence(File(requireNotNull(System.getProperty("user.dir")))) { it.parentFile }
            .first { File(it, "scripts/version/semver-test-vectors.json").isFile }
        val text = File(root, "scripts/version/semver-test-vectors.json").readText()
        vectorPairs(text, "orderedPairs", "equivalentPairs").forEach { pair ->
            assertTrue(SemanticVersion.parse(pair.first) < SemanticVersion.parse(pair.second))
        }
        vectorPairs(text, "equivalentPairs", null).forEach { pair ->
            assertEquals(SemanticVersion.parse(pair.first), SemanticVersion.parse(pair.second))
        }
    }

    private fun vectorPairs(text: String, section: String, nextSection: String?): List<Pair<String, String>> {
        val start = text.indexOf("\"$section\"").also { require(it >= 0) }
        val end = nextSection?.let { text.indexOf("\"$it\"", start).also { index -> require(index > start) } } ?: text.length
        return Regex("""\[\s*"([^"]+)"\s*,\s*"([^"]+)"\s*]""")
            .findAll(text.substring(start, end))
            .map { it.groupValues[1] to it.groupValues[2] }
            .toList()
    }
}
