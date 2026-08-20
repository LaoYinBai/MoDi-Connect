package com.modi.connect.ui.onboarding

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.modi.connect.ui.model.PermissionRequirement

@Composable
fun OnboardingScreen(
    permissionRequirement: PermissionRequirement,
    hasMicrophonePermission: Boolean,
    hasMediaProjection: Boolean,
    batteryOptimizationIgnored: Boolean,
    muteRecoveryPending: Boolean,
    onRequestMicrophone: () -> Unit,
    onRequestMediaProjection: () -> Unit,
    onOpenKeepAliveSettings: () -> Unit,
    onComplete: () -> Unit,
    onSkip: () -> Unit,
    modifier: Modifier = Modifier,
) {
    var state by remember { mutableStateOf(OnboardingState()) }
    val titles = listOf("欢迎使用墨堤互联", "准备电脑与链路", "按需授予音频权限", "保证后台运行与安全恢复")
    val bodies = listOf(
        "墨堤互联把 Android 的麦克风或系统音频低延迟传到 Windows。源码永久以 GPLv3 开源；付费服务提供打包与国内更新。",
        "先在电脑端启动墨堤互联，再让手机和电脑位于同一局域网，或选择万能、蓝牙、USB 链路。主界面会逐步提示所需条件。",
        "麦克风路线需要录音权限；系统音频路线会显示 Android 原生 MediaProjection 授权页。拒绝不会循环弹窗，可稍后再次发起。",
        "推流依赖前台服务。部分厂商还需允许自启动和后台活动。系统音频采集会临时静音手机媒体音量，并用恢复账本处理异常退出。",
    )

    Column(
        modifier.fillMaxSize().padding(horizontal = 24.dp, vertical = 32.dp),
        verticalArrangement = Arrangement.SpaceBetween,
    ) {
        Column {
            Text("${state.stepIndex + 1} / ${OnboardingState.STEP_COUNT}", style = MaterialTheme.typography.labelLarge)
            Spacer(Modifier.height(12.dp))
            Text(titles[state.stepIndex], style = MaterialTheme.typography.headlineMedium)
            Spacer(Modifier.height(16.dp))
            Text(bodies[state.stepIndex], style = MaterialTheme.typography.bodyLarge)
            state.explanation?.let {
                Spacer(Modifier.height(12.dp))
                Text(it, color = MaterialTheme.colorScheme.error)
            }
            if (state.stepIndex == 2) {
                Spacer(Modifier.height(20.dp))
                Text("当前检查：${permissionRequirement.name}", style = MaterialTheme.typography.labelLarge)
                OutlinedButton(onClick = onRequestMicrophone, enabled = !hasMicrophonePermission) {
                    Text(if (hasMicrophonePermission) "麦克风已授权" else "授权麦克风")
                }
                OutlinedButton(onClick = onRequestMediaProjection, enabled = !hasMediaProjection) {
                    Text(if (hasMediaProjection) "系统音频已授权" else "授权系统音频")
                }
            }
            if (state.stepIndex == 3) {
                Spacer(Modifier.height(20.dp))
                Text(if (batteryOptimizationIgnored) "电池优化限制：已放行" else "电池优化限制：建议检查")
                Text(if (muteRecoveryPending) "检测到待恢复的静音账本" else "静音恢复账本：正常")
                OutlinedButton(onClick = onOpenKeepAliveSettings) { Text("打开后台运行设置") }
            }
        }
        Column {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                TextButton(onClick = onSkip) { Text("跳过") }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedButton(onClick = { state = state.back() }, enabled = state.stepIndex > 0) { Text("返回") }
                    Button(onClick = {
                        if (state.stepIndex == OnboardingState.LAST_STEP) onComplete()
                        else state = state.next()
                    }) { Text(if (state.stepIndex == OnboardingState.LAST_STEP) "完成" else "下一步") }
                }
            }
        }
    }
}
