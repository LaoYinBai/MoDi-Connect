package com.modi.connect.ui.device

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.modi.connect.ui.model.LanDevicePanelState
import com.modi.connect.ui.model.LanDeviceUiModel

@Composable
fun LanDevicePanel(
    state: LanDevicePanelState,
    expanded: Boolean,
    onSelectDevice: (LanDeviceUiModel) -> Unit,
    onDismiss: () -> Unit,
) {
    DropdownMenu(
        expanded = expanded,
        onDismissRequest = onDismiss,
        modifier = Modifier.widthIn(min = 288.dp, max = 328.dp),
    ) {
        SectionLabel("当前连接")
        state.connectedDevice?.let { device ->
            DropdownMenuItem(
                text = {
                    DeviceIdentity(device)
                },
                trailingIcon = {
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Box(
                            Modifier
                                .size(8.dp)
                                .background(MaterialTheme.colorScheme.secondary, CircleShape),
                        )
                        Text(
                            "已连接",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.secondary,
                        )
                    }
                },
                onClick = onDismiss,
                modifier = Modifier.semantics {
                    contentDescription =
                        "${device.displayName}，${device.endpointLabel}，已连接"
                },
            )
        } ?: Text(
            "当前未连接",
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 10.dp),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        HorizontalDivider(Modifier.padding(vertical = 4.dp))
        SectionLabel("扫描到的设备")
        if (state.visibleDevices.isEmpty()) {
            Text(
                "正在寻找同一局域网内的电脑",
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        } else {
            state.visibleDevices.forEach { device ->
                val isSelected = state.isSelected(device)
                DropdownMenuItem(
                    text = { DeviceIdentity(device) },
                    trailingIcon = if (isSelected) {
                        {
                            Text(
                                "已选择",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.primary,
                            )
                        }
                    } else {
                        null
                    },
                    onClick = {
                        onSelectDevice(device)
                        onDismiss()
                    },
                    modifier = Modifier.semantics {
                        selected = isSelected
                        contentDescription = buildString {
                            append(device.displayName)
                            append("，")
                            append(device.endpointLabel)
                            if (isSelected) append("，已选择")
                        }
                    },
                )
            }
        }
    }
}

@Composable
private fun SectionLabel(text: String) {
    Text(
        text,
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
        style = MaterialTheme.typography.titleSmall,
        color = MaterialTheme.colorScheme.onSurface,
    )
}

@Composable
private fun DeviceIdentity(device: LanDeviceUiModel) {
    Column {
        Text(
            device.displayName,
            style = MaterialTheme.typography.titleSmall,
            color = MaterialTheme.colorScheme.onSurface,
        )
        Spacer(Modifier.width(4.dp))
        Text(
            device.endpointLabel,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
