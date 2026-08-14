package com.modi.connect.ui.device

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Computer
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.modi.connect.ui.model.LanDevicePanelState
import com.modi.connect.ui.model.LanDeviceUiModel

@Composable
fun LanDeviceButton(
    state: LanDevicePanelState,
    onSelectDevice: (LanDeviceUiModel) -> Unit,
    modifier: Modifier = Modifier,
) {
    var expanded by remember { mutableStateOf(false) }
    val connectionLabel = state.connectedDevice?.let { "当前连接${it.displayName}" } ?: "当前未连接"

    Box(modifier) {
        Surface(
            onClick = { expanded = !expanded },
            modifier = Modifier
                .size(48.dp)
                .semantics {
                    role = Role.Button
                    contentDescription =
                        "局域网电脑设备，$connectionLabel，扫描到${state.visibleDevices.size}台"
                },
            shape = CircleShape,
            color = MaterialTheme.colorScheme.surfaceContainerHigh.copy(alpha = .94f),
            contentColor = MaterialTheme.colorScheme.onSurface,
            border = BorderStroke(
                2.dp,
                if (state.connectedDevice == null) {
                    MaterialTheme.colorScheme.tertiary
                } else {
                    MaterialTheme.colorScheme.secondary
                },
            ),
            tonalElevation = 3.dp,
            shadowElevation = 2.dp,
        ) {
            Box(contentAlignment = Alignment.Center) {
                Icon(
                    Icons.Outlined.Computer,
                    contentDescription = null,
                    modifier = Modifier.size(22.dp),
                )
            }
        }

        LanDevicePanel(
            state = state,
            expanded = expanded,
            onSelectDevice = onSelectDevice,
            onDismiss = { expanded = false },
        )
    }
}
