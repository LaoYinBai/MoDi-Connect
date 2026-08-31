package com.modi.connect.ui.profile

internal enum class ProfileLibrary(val title: String, val asset: String, val splitChapters: Boolean) {
    STORIES("故事汇", "Stories.md", true),
    SPONSORS("赞助榜", "Sponsors.md", false),
    SUPPORT("技术支持", "TechnicalSupport.md", true),
}

internal data class ReaderChapter(val id: String, val title: String, val paragraphs: List<String>)
internal data class ReaderBookmark(val chapterId: String, val block: Int, val offset: Int)
internal data class ReaderPosition(val index: Int, val offset: Int)
internal data class ReaderRow(val chapter: Int, val block: Int, val text: String)

internal class ReaderDocument(val intro: String, val chapters: List<ReaderChapter>) {
    val rows = chapters.flatMapIndexed { chapterIndex, chapter ->
        (listOf(chapter.title) + chapter.paragraphs).mapIndexed { block, text -> ReaderRow(chapterIndex, block, text) }
    }
    val chapterStarts = chapters.indices.map { chapter -> rows.indexOfFirst { it.chapter == chapter } }

    fun restore(bookmark: ReaderBookmark?): ReaderPosition {
        val chapter = chapters.indexOfFirst { it.id == bookmark?.chapterId }
        if (chapter < 0 || bookmark == null) return ReaderPosition(0, 0)
        return ReaderPosition(chapterStarts[chapter] + bookmark.block.coerceIn(0, chapters[chapter].paragraphs.size), bookmark.offset.coerceAtLeast(0))
    }

    fun bookmark(index: Int, offset: Int): ReaderBookmark {
        val row = rows[index.coerceIn(rows.indices)]
        return ReaderBookmark(chapters[row.chapter].id, row.block, offset.coerceAtLeast(0))
    }

    companion object {
        fun parse(markdown: String, splitChapters: Boolean): ReaderDocument {
            val normalized = markdown.replace("\r\n", "\n").trim()
            val title = normalized.lineSequence().firstOrNull()?.takeIf { it.startsWith("# ") }?.drop(2)?.trim() ?: "正文"
            val body = if (normalized.startsWith("# ")) normalized.substringAfter('\n', "").trim() else normalized
            val headings = if (splitChapters) Regex("(?m)^##[ \\t]+(.+)$").findAll(body).toList() else emptyList()
            fun paragraphs(text: String) = text.trim().split(Regex("\\n\\s*\\n")).filter { it.isNotBlank() }
                .ifEmpty { listOf("暂无内容。") }
            if (headings.isEmpty()) return ReaderDocument("", listOf(ReaderChapter(title, title, paragraphs(body))))
            val seen = mutableMapOf<String, Int>()
            return ReaderDocument(body.substring(0, headings.first().range.first).trim(), headings.mapIndexed { index, heading ->
                val headingTitle = heading.groupValues[1].trim()
                val occurrence = seen.getOrDefault(headingTitle, 0)
                seen[headingTitle] = occurrence + 1
                val id = if (occurrence == 0) headingTitle else "$headingTitle#$occurrence"
                val end = headings.getOrNull(index + 1)?.range?.first ?: body.length
                ReaderChapter(id, headingTitle, paragraphs(body.substring(heading.range.last + 1, end)))
            })
        }
    }
}
