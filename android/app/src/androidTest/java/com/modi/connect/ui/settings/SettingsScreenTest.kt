package com.modi.connect.ui.settings

import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import com.modi.connect.ui.theme.MoDiTheme
import org.junit.Rule
import org.junit.Test

class SettingsScreenTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun settings_ui_never_exposes_internal_build_or_commit_identity() {
        composeRule.setContent {
            MoDiTheme(darkTheme = true) {
                SettingsScreen(
                    versionName = "1.0.2",
                    audioConfig = "48 kHz · 128 kbps",
                    streaming = false,
                    developerModeEnabled = true,
                    onDeveloperModeEnabled = {},
                    onBack = {},
                    onExportLogs = {},
                    onNetworkDiagnostics = { "正常" },
                    onOpenKeepAliveSettings = { "已打开" },
                    onClearPairing = { "已清除" },
                    onResetConfiguration = { "已重置" },
                    onResetOnboarding = { "已重置" },
                    onForceDisconnect = { "已断开" },
                    onMessage = {},
                )
            }
        }

        composeRule.onNodeWithText("构建").assertDoesNotExist()
        composeRule.onAllNodesWithText("Build 14 · abc1234", substring = true).assertCountEquals(0)
    }
}
