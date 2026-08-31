package com.modi.connect.ui.settings

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.RadioButton
import androidx.compose.material3.TextButton
import com.modi.connect.ui.theme.LocalThemeSelection
import com.modi.connect.ui.theme.ThemeMode
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

@Composable
fun SettingsScreen(
    versionName: String,
    buildIdentity: String,
    audioConfig: String,
    streaming: Boolean,
    developerModeEnabled: Boolean,
    onDeveloperModeEnabled: () -> Unit,
    onBack: () -> Unit,
    onExportLogs: () -> Unit,
    onNetworkDiagnostics: () -> String,
    onOpenKeepAliveSettings: () -> String,
    onClearPairing: () -> String,
    onResetConfiguration: () -> String,
    onResetOnboarding: () -> String,
    onForceDisconnect: () -> String,
    onMessage: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    var versionTaps by remember { mutableIntStateOf(0) }
    var dangerAction by remember { mutableStateOf<String?>(null) }
    var information by remember { mutableStateOf<Pair<String, String>?>(null) }
    var showAudioWarning by remember { mutableStateOf(false) }
    var showThemePicker by remember { mutableStateOf(false) }
    val theme = LocalThemeSelection.current
    val haptics = LocalHapticFeedback.current

    Column(modifier.fillMaxSize()) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(64.dp)
                .padding(horizontal = 8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Outlined.KeyboardArrowLeft, contentDescription = "返回我的") }
            Text("设置", style = MaterialTheme.typography.headlineSmall)
        }

        Column(
            Modifier
                .weight(1f)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 16.dp, vertical = 4.dp),
            verticalArrangement = Arrangement.spacedBy(24.dp)
        ) {
            SettingsGroup("外观") {
                SettingsRow("主题", theme.mode.label) { showThemePicker = true }
            }

            SettingsGroup("关于") {
                SettingsRow("版本号", versionName, showArrow = false) {
                    if (!developerModeEnabled) {
                        versionTaps++
                        when {
                            versionTaps in 3..4 -> onMessage("再点击 ${5 - versionTaps} 次即可进入开发者模式")
                            versionTaps >= 5 -> {
                                onDeveloperModeEnabled()
                                haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                                onMessage("您已处于开发者模式")
                            }
                        }
                    } else onMessage("您已处于开发者模式")
                }
                SettingsRow("构建", buildIdentity, showArrow = false) { }
                SettingsRow("开源协议", "GPL v3") {
                    information = "开源协议" to "墨堤互联应用层以 GNU General Public License v3.0 发布。阿里妈妈东方大楷按厂商许可原样打包；霞鹜文楷、朱雀仿宋、源樣明體与思源宋体修改子集使用 SIL OFL 1.1。五份完整字体许可随应用提供。"
                }
            }

            SettingsGroup("调试") {
                SettingsRow("日志导出") { onExportLogs() }
                SettingsRow("网络诊断") { information = "网络诊断" to onNetworkDiagnostics() }
                SettingsRow("后台运行设置") { onMessage(onOpenKeepAliveSettings()) }
            }

            SettingsGroup("数据") {
                SettingsRow("清除配对记录", color = MaterialTheme.colorScheme.error) { dangerAction = "清除配对记录" }
                SettingsRow("重新显示新手引导") { onMessage(onResetOnboarding()) }
                SettingsRow("重置配置", color = MaterialTheme.colorScheme.error) { dangerAction = "重置配置" }
            }

            if (developerModeEnabled) {
                SettingsGroup("开发者选项") {
                    SettingsRow("音频参数", audioConfig) {
                        if (streaming) showAudioWarning = true
                        else information = "音频参数" to audioConfig
                    }
                    SettingsRow("强制断连") { onMessage(onForceDisconnect()) }
                    SettingsRow("日志级别", "信息") { onMessage("日志级别由当前构建固定为信息") }
                }
            }
            Spacer(Modifier.height(24.dp))
        }
    }

    if (showThemePicker) {
        AlertDialog(
            onDismissRequest = { showThemePicker = false },
            title = { Text("主题") },
            text = {
                Column(Modifier.selectableGroup()) {
                    ThemeMode.entries.forEach { mode ->
                        Row(Modifier.fillMaxWidth().height(56.dp)
                            .selectable(selected = theme.mode == mode, role = Role.RadioButton, onClick = { theme.select(mode) }),
                            verticalAlignment = Alignment.CenterVertically) {
                            RadioButton(selected = theme.mode == mode, onClick = null)
                            Text(mode.label, Modifier.padding(start = 12.dp), style = MaterialTheme.typography.bodyLarge)
                        }
                    }
                }
            },
            confirmButton = { TextButton(onClick = { showThemePicker = false }) { Text("完成") } },
        )
    }

    dangerAction?.let { action ->
        DangerConfirmationDialog(
            actionName = action,
            onConfirm = {
                onMessage(if (action == "清除配对记录") onClearPairing() else onResetConfiguration())
                dangerAction = null
            },
            onDismiss = { dangerAction = null }
        )
    }
    if (showAudioWarning) {
        AudioSettingWarningDialog(
            onConfirm = {
                onMessage(onForceDisconnect())
                information = "音频参数" to audioConfig
                showAudioWarning = false
            },
            onDismiss = { showAudioWarning = false }
        )
    }
    information?.let { (title, message) ->
        InformationDialog(title, message, onDismiss = { information = null })
    }
}

@Composable
private fun SettingsGroup(title: String, content: @Composable () -> Unit) {
    Column {
        Text(
            text = title,
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(start = 12.dp, bottom = 8.dp)
        )
        Surface(shape = RoundedCornerShape(12.dp), color = MaterialTheme.colorScheme.surfaceContainer) {
            Column { content() }
        }
    }
}

@Composable
private fun SettingsRow(
    title: String,
    detail: String? = null,
    showArrow: Boolean = true,
    color: Color = MaterialTheme.colorScheme.onSurface,
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(56.dp)
            .clickable(role = Role.Button, onClick = onClick)
            .padding(horizontal = 16.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(title, style = MaterialTheme.typography.titleSmall, color = color, modifier = Modifier.weight(1f))
        detail?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
        if (showArrow) Icon(Icons.AutoMirrored.Outlined.KeyboardArrowRight, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
    }
    HorizontalDivider(modifier = Modifier.padding(start = 16.dp), color = MaterialTheme.colorScheme.outlineVariant)
}
