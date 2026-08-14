package com.modi.connect.ui.theme

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.nio.file.Files
import java.nio.file.Path

class FontArtifactContractTest {

    private val repositoryRoot: Path = Path.of("../..").toAbsolutePath().normalize()
    private val sharedFontRoot: Path = repositoryRoot.resolve("assets/fonts/android-res/font")
    private val sharedLicenseRoot: Path = repositoryRoot.resolve("assets/fonts/android-res/raw")

    @Test
    fun `android consumes exactly the five shared font artifacts`() {
        val expected = setOf(
            "modi_title_alimama_dongfang_dakai.ttf",
            "modi_function_lxgw_wenkai.ttf",
            "modi_body_zhuque_fangsong.ttf",
            "modi_annotation_genyo_mincho.otf",
            "modi_default_source_han_serif.otf",
        )

        val actual = Files.list(sharedFontRoot).use { paths ->
            paths.filter(Files::isRegularFile).map { it.fileName.toString() }.toList().toSet()
        }

        assertEquals(expected, actual)
        val buildScript = String(
            Files.readAllBytes(repositoryRoot.resolve("android/app/build.gradle.kts")),
        )
        assertTrue(buildScript.contains("assets/fonts/android-res"))
    }

    @Test
    fun `legacy android font resources are removed`() {
        val legacyRoot = repositoryRoot.resolve("android/app/src/main/res/font")
        listOf(
            "fandol_fang.otf",
            "lxgw_wenkai_lite.ttf",
            "noto_sans_sc.ttf",
            "noto_serif_sc.ttf",
        ).forEach { fileName ->
            assertFalse("legacy font remains: $fileName", Files.exists(legacyRoot.resolve(fileName)))
        }
    }

    @Test
    fun `android prebuild verifies fonts and packages exactly five current licenses`() {
        val buildScript = String(
            Files.readAllBytes(repositoryRoot.resolve("android/app/build.gradle.kts")),
        )
        assertTrue(buildScript.contains("verifyFontArtifacts"))
        assertTrue(buildScript.contains("scripts/fonts/verify_fonts.py"))
        assertTrue(buildScript.contains("dependsOn(verifyFontArtifacts)"))

        val expected = setOf(
            "alimama_dongfang_dakai_license.txt",
            "lxgw_wenkai_ofl.txt",
            "zhuque_fangsong_ofl.txt",
            "genyo_mincho_ofl.txt",
            "source_han_serif_ofl.txt",
        )
        val actual = Files.list(sharedLicenseRoot).use { paths ->
            paths.filter(Files::isRegularFile).map { it.fileName.toString() }.toList().toSet()
        }
        assertEquals(expected, actual)

        listOf("fandol_copying.txt", "lxgw_wenkai_lite_ofl.txt", "noto_ofl.txt").forEach { fileName ->
            assertFalse(
                "legacy font license remains: $fileName",
                Files.exists(repositoryRoot.resolve("android/app/src/main/res/raw/$fileName")),
            )
        }
    }

    @Test
    fun `all material typography slots belong to the five font language`() {
        assertEquals(MoDiFontFamilies.title, MoDiTypography.displayLarge.fontFamily)
        assertEquals(MoDiFontFamilies.default, MoDiTypography.displayMedium.fontFamily)
        assertEquals(MoDiFontFamilies.title, MoDiTypography.displaySmall.fontFamily)
        assertEquals(MoDiFontFamilies.default, MoDiTypography.headlineLarge.fontFamily)
        assertEquals(MoDiFontFamilies.default, MoDiTypography.headlineMedium.fontFamily)
        assertEquals(MoDiFontFamilies.title, MoDiTypography.headlineSmall.fontFamily)
        assertEquals(MoDiFontFamilies.function, MoDiTypography.titleLarge.fontFamily)
        assertEquals(MoDiFontFamilies.function, MoDiTypography.titleMedium.fontFamily)
        assertEquals(MoDiFontFamilies.function, MoDiTypography.titleSmall.fontFamily)
        assertEquals(MoDiFontFamilies.body, MoDiTypography.bodyLarge.fontFamily)
        assertEquals(MoDiFontFamilies.body, MoDiTypography.bodyMedium.fontFamily)
        assertEquals(MoDiFontFamilies.annotation, MoDiTypography.bodySmall.fontFamily)
        assertEquals(MoDiFontFamilies.function, MoDiTypography.labelLarge.fontFamily)
        assertEquals(MoDiFontFamilies.function, MoDiTypography.labelMedium.fontFamily)
        assertEquals(MoDiFontFamilies.function, MoDiTypography.labelSmall.fontFamily)
    }
}
