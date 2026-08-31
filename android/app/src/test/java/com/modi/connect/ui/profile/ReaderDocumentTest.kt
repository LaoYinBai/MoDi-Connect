package com.modi.connect.ui.profile

import org.junit.Assert.*
import org.junit.Test

class ReaderDocumentTest {
    @Test fun chaptersKeepSubheadingsAndIntroWithoutCreatingEmptyChapters() {
        val document = ReaderDocument.parse("# 故事汇\n\n简介\n\n## 第一章\n\n一段\n\n### 小节\n\n二段\n\n## 第二章\n\n三段", splitChapters = true)
        assertEquals("简介", document.intro)
        assertEquals(listOf("第一章", "第二章"), document.chapters.map { it.title })
        assertTrue(document.chapters[0].paragraphs.contains("### 小节"))
        assertEquals("三段", document.chapters[1].paragraphs.single())
    }

    @Test fun sponsorsStayOneCompleteDocument() {
        val document = ReaderDocument.parse("# 全部赞助名单\n\n说明\n\n## 支持者\n\n甲\n\n乙", splitChapters = false)
        assertEquals(1, document.chapters.size)
        assertEquals("全部赞助名单", document.chapters.single().title)
        assertTrue(document.chapters.single().paragraphs.contains("乙"))
    }

    @Test fun restoreClampsRemovedParagraphsAndUnknownChapters() {
        val document = ReaderDocument.parse("# 总标题\n\n## 一\n\n段落\n\n## 二\n\n正文", true)
        assertEquals(ReaderPosition(0, 0), document.restore(ReaderBookmark("不存在", 900, -8)))
        val second = document.chapters[1]
        assertEquals(ReaderPosition(3, 20), document.restore(ReaderBookmark(second.id, 99, 20)))
        assertEquals(ReaderBookmark(second.id, 1, 20), document.bookmark(3, 20))
    }

    @Test fun emptyDocumentStillHasAReadableFallback() {
        val document = ReaderDocument.parse("", true)
        assertEquals(1, document.chapters.size)
        assertTrue(document.chapters.single().paragraphs.isNotEmpty())
    }
}
