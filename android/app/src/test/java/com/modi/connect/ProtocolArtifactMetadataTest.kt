package com.modi.connect

import com.modi.protocol.PacketHeaderCodec
import java.io.File
import java.security.MessageDigest
import java.util.jar.JarFile
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ProtocolArtifactMetadataTest {
    @Test
    fun resolvedProtocolComesFromThePinnedJarWithMatchingReleaseIdentity() {
        val manifest = repositoryFile("third_party/modi-protocol/protocol-artifacts.v1.json").readText()
        assertEquals("0.1.1", jsonString(manifest, "protocolVersion"))
        assertEquals("PROPRIETARY_SOURCE_OWNER_ISSUED", jsonString(manifest, "sourceLicenseStatus"))
        assertEquals("EXTERNAL_DISTRIBUTION_APPROVED_BY_OWNER", jsonString(manifest, "externalDistributionStatus"))
        val expectedCommit = jsonString(manifest, "sourceCommit")
        val expectedVectorSha = Regex("\"vectorSet\"\\s*:\\s*\\{[^}]*\"sha256\"\\s*:\\s*\"([0-9a-f]{64})\"")
            .find(manifest)?.groupValues?.get(1) ?: error("Missing vector-set SHA-256")
        val jarRecord = Regex("\\{[^{}]*\\}", RegexOption.DOT_MATCHES_ALL)
            .findAll(manifest)
            .map { it.value }
            .firstOrNull { it.contains(Regex("\"path\"\\s*:\\s*\"[^\"]+\\.jar\"")) }
            ?: error("Missing JAR artifact record")
        val vendoredJarRelativePath = jsonString(jarRecord, "path")
        val expectedJarSha = jsonString(jarRecord, "sha256")
        val vendoredJar = repositoryFile("third_party/modi-protocol/$vendoredJarRelativePath")

        val codeSource = requireNotNull(PacketHeaderCodec::class.java.protectionDomain?.codeSource)
        val jarPath = File(codeSource.location.toURI())
        assertTrue("Protocol code source must be a JAR: $jarPath", jarPath.isFile && jarPath.extension.equals("jar", ignoreCase = true))
        assertFalse("Protocol must not load from a directory", jarPath.isDirectory)
        assertEquals(expectedJarSha, sha256(vendoredJar))
        assertEquals("Resolved protocol JAR must be byte-identical to the canonical vendored JAR", expectedJarSha, sha256(jarPath))

        JarFile(jarPath).use { jar ->
            val attributes = jar.manifest.mainAttributes
            assertEquals("0.1.1", attributes.getValue("Implementation-Version"))
            assertEquals(expectedCommit, attributes.getValue("MoDi-Protocol-Commit"))
            assertEquals(expectedVectorSha, attributes.getValue("MoDi-Protocol-Vector-SHA256"))
            assertTrue(jar.getJarEntry("com/modi/protocol/PacketHeaderCodec.class") != null)
            assertTrue(jar.getJarEntry("META-INF/PROPRIETARY-PROTOCOL-LICENSE-1.0.txt") != null)
            assertTrue(jar.getJarEntry("META-INF/BINARY-REDISTRIBUTION-GRANT-1.0.txt") != null)
            assertTrue(jar.getJarEntry("META-INF/MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt") != null)
            assertTrue(jar.getJarEntry("META-INF/THIRD-PARTY-NOTICES.md") != null)
            assertTrue(jar.entries().asSequence().none { it.name.endsWith(".kt") || it.name.startsWith("com/modi/connect/") })
        }
    }

    private fun repositoryFile(relativePath: String): File {
        var directory: File? = File(requireNotNull(System.getProperty("user.dir"))).absoluteFile
        while (directory != null) {
            val candidate = File(directory, relativePath)
            if (candidate.isFile) return candidate
            directory = directory.parentFile
        }
        error("Cannot locate repository file: $relativePath")
    }

    private fun jsonString(json: String, property: String): String =
        Regex("\"${Regex.escape(property)}\"\\s*:\\s*\"([^\"]+)\"")
            .find(json)?.groupValues?.get(1) ?: error("Missing manifest property: $property")

    private fun sha256(file: File): String {
        val digest = MessageDigest.getInstance("SHA-256")
        file.inputStream().buffered().use { input ->
            val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
            while (true) {
                val count = input.read(buffer)
                if (count < 0) break
                digest.update(buffer, 0, count)
            }
        }
        return digest.digest().joinToString("") { "%02x".format(it) }
    }
}
