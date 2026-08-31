package com.modi.connect.ui.profile

import android.content.Context
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.LinkAnnotation
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextLinkStyles
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.filter
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@Composable
internal fun ProfileReaderScreen(library: ProfileLibrary, onClose: () -> Unit) {
    val context = LocalContext.current.applicationContext
    val document by produceState<ReaderDocument?>(null, library, context) {
        value = withContext(Dispatchers.IO) {
            val markdown = runCatching { context.assets.open("content/${library.asset}").bufferedReader().use { it.readText() } }
                .getOrElse { "# ${library.title}\n\n内容暂时无法读取，请退出后重试。" }
            ReaderDocument.parse(markdown, library.splitChapters)
        }
    }
    val loaded = document
    if (loaded == null) {
        BackHandler(onBack = onClose)
        Box(Modifier.fillMaxSize().systemBarsPadding(), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
    } else key(library) { ReaderContent(library, loaded, onClose) }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ReaderContent(library: ProfileLibrary, document: ReaderDocument, onClose: () -> Unit) {
    val context = LocalContext.current.applicationContext
    val preferences = remember(context) { context.getSharedPreferences("profile-reading", Context.MODE_PRIVATE) }
    val prefix = library.name
    val savedBookmark = remember(library, preferences) {
        preferences.getString("$prefix.chapter", null)?.let {
            ReaderBookmark(it, preferences.getInt("$prefix.block", 0), preferences.getInt("$prefix.offset", 0))
        }
    }
    val initial = remember(document, savedBookmark) { document.restore(savedBookmark) }
    val scroll = rememberLazyListState(initial.index, initial.offset)
    var reading by rememberSaveable { mutableStateOf(false) }
    var showDirectory by rememberSaveable { mutableStateOf(false) }
    var hasProgress by rememberSaveable { mutableStateOf(savedBookmark != null) }
    val scope = rememberCoroutineScope()
    val chapterIndex by remember(document, scroll) {
        derivedStateOf { document.rows[scroll.firstVisibleItemIndex.coerceIn(document.rows.indices)].chapter }
    }

    fun savePosition() {
        if (!hasProgress) return
        val bookmark = document.bookmark(scroll.firstVisibleItemIndex, scroll.firstVisibleItemScrollOffset)
        preferences.edit().putString("$prefix.chapter", bookmark.chapterId)
            .putInt("$prefix.block", bookmark.block).putInt("$prefix.offset", bookmark.offset).apply()
    }
    // Persist settled positions, not every pixel of a fling. Save once more when leaving.
    LaunchedEffect(scroll, hasProgress) {
        snapshotFlow { Triple(scroll.firstVisibleItemIndex, scroll.firstVisibleItemScrollOffset, scroll.isScrollInProgress) }
            .filter { !it.third }.collect { savePosition() }
    }
    DisposableEffect(document, hasProgress) { onDispose { savePosition() } }

    fun openChapter(index: Int, keepPosition: Boolean = false) {
        reading = true
        hasProgress = true
        showDirectory = false
        if (!keepPosition) scope.launch { scroll.scrollToItem(document.chapterStarts[index]) }
    }
    fun back() {
        savePosition()
        if (reading) reading = false else onClose()
    }
    BackHandler { back() }

    Column(Modifier.fillMaxSize().systemBarsPadding()) {
        Row(Modifier.fillMaxWidth().heightIn(min = 64.dp).padding(horizontal = 8.dp), verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = ::back) {
                Icon(Icons.AutoMirrored.Outlined.KeyboardArrowLeft, if (reading) "返回目录" else "返回我的")
            }
            Column(Modifier.weight(1f).padding(vertical = 8.dp)) {
                Text(if (reading) document.chapters[chapterIndex].title else library.title,
                    style = MaterialTheme.typography.titleLarge, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text(if (reading) "${library.title} · ${chapterIndex + 1} / ${document.chapters.size}" else "目录 · 共 ${document.chapters.size} 篇",
                    style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        if (reading) {
            LazyColumn(state = scroll, modifier = Modifier.weight(1f).fillMaxWidth(), contentPadding = PaddingValues(start = 24.dp, end = 24.dp, bottom = 20.dp)) {
                itemsIndexed(document.rows, key = { _, row -> "${document.chapters[row.chapter].id}:${row.block}" }) { _, row ->
                    if (row.block == 0) {
                        if (row.chapter > 0) HorizontalDivider(Modifier.padding(top = 24.dp), color = MaterialTheme.colorScheme.outlineVariant)
                        Text(row.text, Modifier.padding(top = 20.dp, bottom = 24.dp), style = MaterialTheme.typography.headlineSmall)
                    } else ReaderParagraph(row.text)
                }
                // An end page leaves enough scroll range for even the shortest last
                // chapter to align at the top when selected from the directory.
                item {
                    Box(Modifier.fillParentMaxHeight().fillMaxWidth().padding(top = 32.dp), contentAlignment = Alignment.TopCenter) {
                        Text("— 已读完${library.title} —", style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
            Surface(color = MaterialTheme.colorScheme.surfaceContainer) {
                Row(Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    TextButton(onClick = { openChapter(chapterIndex - 1) }, enabled = chapterIndex > 0) { Text("上一章") }
                    TextButton(onClick = { showDirectory = true }) { Text("目录") }
                    TextButton(onClick = { openChapter(chapterIndex + 1) }, enabled = chapterIndex < document.chapters.lastIndex) { Text("下一章") }
                }
            }
        } else {
            LazyColumn(Modifier.weight(1f).fillMaxWidth(), contentPadding = PaddingValues(16.dp)) {
                if (document.intro.isNotBlank()) item {
                    Text(ProfileContentText.fromMarkdown(document.intro), Modifier.padding(12.dp),
                        style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                if (hasProgress) item {
                    TextButton(onClick = { openChapter(chapterIndex, keepPosition = true) }) {
                        Text("继续阅读 · ${document.chapters[chapterIndex].title}")
                    }
                }
                itemsIndexed(document.chapters, key = { _, chapter -> chapter.id }) { index, chapter ->
                    ChapterEntry(index, chapter, current = hasProgress && index == chapterIndex) {
                        openChapter(index, keepPosition = hasProgress && index == chapterIndex)
                    }
                }
            }
        }
    }
    if (showDirectory) {
        ModalBottomSheet(onDismissRequest = { showDirectory = false }, containerColor = MaterialTheme.colorScheme.surfaceContainer) {
            Text("${library.title} · 目录", Modifier.padding(horizontal = 24.dp, vertical = 12.dp), style = MaterialTheme.typography.titleLarge)
            val directoryScroll = rememberLazyListState(initialFirstVisibleItemIndex = chapterIndex)
            LazyColumn(state = directoryScroll, modifier = Modifier.fillMaxWidth().weight(1f, fill = false), contentPadding = PaddingValues(16.dp)) {
                itemsIndexed(document.chapters, key = { _, chapter -> chapter.id }) { index, chapter ->
                    ChapterEntry(index, chapter, current = index == chapterIndex) { openChapter(index, keepPosition = index == chapterIndex) }
                }
            }
        }
    }
}

@Composable
private fun ChapterEntry(index: Int, chapter: ReaderChapter, current: Boolean, onClick: () -> Unit) {
    Surface(Modifier.fillMaxWidth().padding(vertical = 6.dp), shape = MaterialTheme.shapes.medium,
        color = if (current) MaterialTheme.colorScheme.surfaceContainerHigh else MaterialTheme.colorScheme.surfaceContainer) {
        Row(Modifier.clickable(role = Role.Button, onClick = onClick).padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Text("%02d".format(index + 1), Modifier.padding(end = 16.dp), style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.primary)
            Column(Modifier.weight(1f)) {
                Text(chapter.title, style = MaterialTheme.typography.titleMedium)
                Text(ProfileContentText.fromMarkdown(chapter.paragraphs.first()), Modifier.padding(top = 6.dp),
                    style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2, overflow = TextOverflow.Ellipsis)
                if (current) Text("阅读至此", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary)
            }
            Icon(Icons.AutoMirrored.Outlined.KeyboardArrowRight, null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun ReaderParagraph(markdown: String) {
    val text = ProfileContentText.fromMarkdown(markdown).replace("`", "")
    val linkColor = MaterialTheme.colorScheme.primary
    val annotated = remember(text, linkColor) {
        buildAnnotatedString {
            append(text)
            Regex("https://[^\\s）)]+").findAll(text).forEach { link ->
                addLink(LinkAnnotation.Url(link.value, TextLinkStyles(style = SpanStyle(color = linkColor))), link.range.first, link.range.last + 1)
            }
        }
    }
    SelectionContainer {
        Text(annotated, Modifier.fillMaxWidth().padding(bottom = 18.dp),
            style = MaterialTheme.typography.bodyLarge.copy(fontSize = 18.sp, lineHeight = 32.sp),
            fontWeight = if (markdown.startsWith("#")) FontWeight.SemiBold else FontWeight.Normal,
            color = if (markdown.startsWith("> ")) MaterialTheme.colorScheme.onSurfaceVariant else MaterialTheme.colorScheme.onSurface)
    }
}
