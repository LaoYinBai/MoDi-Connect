package com.modi.connect.ui.link

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Bluetooth
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Language
import androidx.compose.material.icons.outlined.Usb
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.modi.connect.ui.model.LinkChoice
import com.modi.connect.ui.model.LinkUiState

fun linkSelectionMenuItems(): List<LinkChoice> = listOf(
    LinkChoice.HOME,
    LinkChoice.UNIVERSAL,
    LinkChoice.BLUETOOTH,
    LinkChoice.USB
)

@Composable
fun LinkSelectionMenu(
    state: LinkUiState,
    expanded: Boolean,
    onSelect: (LinkChoice) -> Unit,
    onDismiss: () -> Unit
) {
    DropdownMenu(expanded = expanded, onDismissRequest = onDismiss) {
        linkSelectionMenuItems().forEach { choice ->
            val isSelected = state.selected == choice
            val isActive = state.active == choice
            DropdownMenuItem(
                text = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column {
                            Text(choice.title, style = MaterialTheme.typography.titleSmall)
                            Text(
                                choice.environment,
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                        Spacer(Modifier.width(16.dp))
                        if (isSelected || isActive) {
                            Text(
                                when {
                                    isSelected && isActive -> "已选择 · 使用中"
                                    isActive -> "使用中"
                                    else -> "已选择"
                                },
                                style = MaterialTheme.typography.labelMedium,
                                color = if (isActive) MaterialTheme.colorScheme.secondary else MaterialTheme.colorScheme.primary
                            )
                        }
                    }
                },
                leadingIcon = {
                    Icon(linkIcon(choice), contentDescription = null)
                },
                onClick = {
                    onSelect(choice)
                    onDismiss()
                },
                modifier = Modifier
                    .width(280.dp)
                    .semantics { selected = isSelected }
                    .padding(vertical = 2.dp)
            )
        }
    }
}

internal fun linkIcon(choice: LinkChoice): ImageVector = when (choice) {
    LinkChoice.HOME -> Icons.Outlined.Home
    LinkChoice.UNIVERSAL -> Icons.Outlined.Language
    LinkChoice.BLUETOOTH -> Icons.Outlined.Bluetooth
    LinkChoice.USB -> Icons.Outlined.Usb
}
