package com.modi.connect.ui.settings

import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.text.TextStyle

@Composable
fun DangerConfirmationDialog(
    actionName: String,
    onConfirm: () -> Unit,
    onDismiss: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("确认操作") },
        text = { Text("确定要${actionName}吗？此操作不可撤销。") },
        confirmButton = {
            TextButton(onClick = onConfirm) { Text("确认", color = MaterialTheme.colorScheme.error) }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("取消") } }
    )
}

@Composable
fun AudioSettingWarningDialog(onConfirm: () -> Unit, onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("停止推流并查看参数？") },
        text = { Text("音频参数在当前版本中由音频引擎固定。继续将停止当前推流，查看参数后需要手动重新连接。") },
        confirmButton = { TextButton(onClick = onConfirm) { Text("停止并继续") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("取消") } }
    )
}

@Composable
fun InformationDialog(
    title: String,
    message: String,
    onDismiss: () -> Unit,
    messageStyle: TextStyle = MaterialTheme.typography.bodyMedium
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = { Text(message, style = messageStyle) },
        confirmButton = { TextButton(onClick = onDismiss) { Text("知道了") } }
    )
}
