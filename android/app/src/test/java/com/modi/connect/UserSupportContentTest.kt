package com.modi.connect

import com.modi.connect.ui.profile.ProfileContentText
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class UserSupportContentTest {
    @Test
    fun profileContentIsBundledForEveryOfflineEntry() {
        listOf("Stories.md", "Sponsors.md", "TechnicalSupport.md").forEach { name ->
            val content = repositoryFile("android/app/src/main/assets/content/$name").readText()
            assertTrue("Bundled profile content is too short: $name", content.length > 100)
        }
    }

    @Test
    fun profileMarkdownBecomesReadableDialogText() {
        val rendered = ProfileContentText.fromMarkdown("# 标题\n\n## 小节\n\n- 条目\n\n[官网](https://modiconnect.cn)")
        assertEquals("标题\n\n小节\n\n• 条目\n\n官网（https://modiconnect.cn）", rendered)
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
}
