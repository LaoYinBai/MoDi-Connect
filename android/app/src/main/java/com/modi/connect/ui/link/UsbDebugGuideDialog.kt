package com.modi.connect.ui.link

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

@Composable
fun UsbDebugGuideDialog(
    onContinue: () -> Unit,
    onLater: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onLater,
        title = { Text("启用 USB 调试") },
        text = {
            Column {
                Text("USB 是兜底链路，需要先完成一次系统设置：")
                Text("1. 在手机“开发者选项”中开启 USB 调试", Modifier.padding(top = 10.dp))
                Text("2. 使用支持数据传输的 USB 线连接电脑")
                Text("3. 手机弹出授权窗口时选择“允许 USB 调试”")
                Text("这不会影响 LAN、万能或蓝牙链路。", Modifier.padding(top = 10.dp))
            }
        },
        confirmButton = {
            TextButton(onClick = onContinue) { Text("我已开启，继续") }
        },
        dismissButton = {
            TextButton(onClick = onLater) { Text("稍后设置") }
        },
    )
}
