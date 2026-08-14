package com.modi.connect.ui.navigation

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.GraphicEq
import androidx.compose.material.icons.outlined.PersonOutline
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable

enum class AppDestination { AUDIO, PROFILE, SETTINGS }

@Composable
fun AppBottomNavigation(selected: AppDestination, onSelect: (AppDestination) -> Unit) {
    NavigationBar(containerColor = MaterialTheme.colorScheme.surfaceContainer) {
        NavigationBarItem(
            selected = selected == AppDestination.AUDIO,
            onClick = { onSelect(AppDestination.AUDIO) },
            icon = { Icon(Icons.Outlined.GraphicEq, contentDescription = null) },
            label = { Text("音频", style = MaterialTheme.typography.labelMedium) },
            colors = NavigationBarItemDefaults.colors(indicatorColor = MaterialTheme.colorScheme.primaryContainer)
        )
        NavigationBarItem(
            selected = selected == AppDestination.PROFILE || selected == AppDestination.SETTINGS,
            onClick = { onSelect(AppDestination.PROFILE) },
            icon = { Icon(Icons.Outlined.PersonOutline, contentDescription = null) },
            label = { Text("我的", style = MaterialTheme.typography.labelMedium) },
            colors = NavigationBarItemDefaults.colors(indicatorColor = MaterialTheme.colorScheme.primaryContainer)
        )
    }
}
