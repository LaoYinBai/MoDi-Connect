package com.modi.connect.ui.theme

import android.content.Context
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalContext

data class ThemeSelection(val mode: ThemeMode, val select: (ThemeMode) -> Unit)
val LocalThemeSelection = staticCompositionLocalOf { ThemeSelection(ThemeMode.SYSTEM) {} }

@Composable
fun ThemePreferenceProvider(content: @Composable () -> Unit) {
    val context = LocalContext.current.applicationContext
    val preferences = remember(context) { context.getSharedPreferences("appearance", Context.MODE_PRIVATE) }
    var mode by remember(preferences) { mutableStateOf(ThemeMode.fromStored(preferences.getString("theme", null))) }
    val selection = ThemeSelection(mode) { selected ->
        preferences.edit().putString("theme", selected.storedValue).apply()
        mode = selected
    }
    CompositionLocalProvider(LocalThemeSelection provides selection, content = content)
}
