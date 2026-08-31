/*
 * MoDi Connect - Cross-device interconnection protocol
 * Copyright (C) 2026 Silvite
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
package com.modi.connect

import android.os.Bundle
import android.view.KeyEvent
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.runtime.SideEffect
import androidx.core.view.WindowCompat
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import com.modi.connect.ui.MoDiApp
import com.modi.connect.ui.theme.MoDiTheme
import com.modi.connect.ui.theme.ThemePreferenceProvider
import com.modi.connect.ui.theme.LocalThemeSelection
import com.modi.connect.ui.runtime.MoDiRuntime

/**
 * MainActivity — 启动入口 + 组装
 *
 * 职责仅三件事：
 *   1. 组装 — 启动 MoDiApp 组合根
 *   2. 注入 — 由 MoDiApp 管理 UI 运行时与权限
 *   3. 启动 — setContent 启动界面
 *
 * 不含任何链路逻辑、UI 布局、权限申请代码。
 */
class MainActivity : ComponentActivity() {
    private var runtime: MoDiRuntime? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            ThemePreferenceProvider {
                val dark = LocalThemeSelection.current.mode.isDark(isSystemInDarkTheme())
                SideEffect {
                    WindowCompat.getInsetsController(window, window.decorView).apply {
                        isAppearanceLightStatusBars = !dark
                        isAppearanceLightNavigationBars = !dark
                    }
                }
                MoDiTheme(darkTheme = dark) {
                    Surface(
                        modifier = Modifier.fillMaxSize(),
                        color = MaterialTheme.colorScheme.background
                    ) { MoDiApp { runtime = it } }
                }
            }
        }
    }

    override fun onKeyDown(keyCode: Int, event: KeyEvent?): Boolean = when (keyCode) {
        KeyEvent.KEYCODE_VOLUME_UP -> runtime?.adjustStreamVolumeUp() == true || super.onKeyDown(keyCode, event)
        KeyEvent.KEYCODE_VOLUME_DOWN -> runtime?.adjustStreamVolumeDown() == true || super.onKeyDown(keyCode, event)
        else -> super.onKeyDown(keyCode, event)
    }
}
