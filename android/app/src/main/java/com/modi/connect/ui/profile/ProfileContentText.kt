package com.modi.connect.ui.profile

internal object ProfileContentText {
    private val markdownLink = Regex("\\[([^]]+)]\\(([^)]+)\\)")

    fun fromMarkdown(markdown: String): String = markdown
        .lineSequence()
        .map { line ->
            val withoutHeading = line.replace(Regex("^#{1,6}\\s+"), "")
            val withReadableLinks = markdownLink.replace(withoutHeading) { match ->
                "${match.groupValues[1]}（${match.groupValues[2]}）"
            }
            when {
                withReadableLinks.startsWith("- ") -> "• ${withReadableLinks.drop(2)}"
                withReadableLinks.startsWith("> ") -> withReadableLinks.drop(2)
                else -> withReadableLinks
            }
        }
        .joinToString("\n")
        .trim()
}
