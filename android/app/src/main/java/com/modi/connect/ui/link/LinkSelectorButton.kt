package com.modi.connect.ui.link

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.modi.connect.ConnectionState
import com.modi.connect.ui.model.LinkChoice
import com.modi.connect.ui.model.LinkUiState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue

@Composable
fun LinkSelectorButton(
    state: LinkUiState,
    onSelect: (LinkChoice) -> Unit,
    modifier: Modifier = Modifier
) {
    var expanded by remember { mutableStateOf(false) }
    Box(modifier) {
        val ringColor = linkStateColor(state.connectionState)
        Surface(
            onClick = { expanded = !expanded },
            modifier = Modifier
                .size(48.dp)
                .semantics {
                    role = Role.Button
                    contentDescription = "切换链路，当前目标${state.selected.title}"
                    stateDescription = "${state.activeLabel}，${state.statusMessage}"
                },
            shape = CircleShape,
            color = MaterialTheme.colorScheme.surfaceContainerHigh.copy(alpha = .94f),
            contentColor = MaterialTheme.colorScheme.onSurface,
            border = BorderStroke(2.dp, ringColor),
            tonalElevation = 3.dp,
            shadowElevation = 2.dp
        ) {
            Icon(
                linkIcon(state.selected),
                contentDescription = null,
                modifier = Modifier.size(22.dp)
            )
        }
        LinkSelectionMenu(
            state = state,
            expanded = expanded,
            onSelect = onSelect,
            onDismiss = { expanded = false }
        )
    }
}

@Composable
private fun linkStateColor(state: ConnectionState): Color = when (state) {
    ConnectionState.CONNECTING, ConnectionState.RECONNECTING -> MaterialTheme.colorScheme.primary
    ConnectionState.CONNECTED, ConnectionState.STREAMING -> MaterialTheme.colorScheme.secondary
    ConnectionState.ERROR -> MaterialTheme.colorScheme.error
    else -> MaterialTheme.colorScheme.tertiary
}
