package com.modi.connect.ui.runtime

import android.content.Context
import android.content.Intent
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import com.modi.connect.audio.AudioConfig
import com.modi.connect.core.impl.ExportableLogger

class DiagnosticsFacade(
    private val context: Context,
    private val targetName: () -> String?,
    private val deviceCount: () -> Int,
    private val status: () -> String,
    private val selectedLink: () -> String,
    private val activeLink: () -> String,
) {
    fun networkDiagnostics(): String {
        val manager = context.getSystemService(ConnectivityManager::class.java)
        val network = manager.activeNetwork ?: return "当前没有可用网络"
        val capabilities = manager.getNetworkCapabilities(network) ?: return "无法读取当前网络能力"
        val transport = when {
            capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) -> "Wi-Fi"
            capabilities.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) -> "蜂窝网络"
            capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) -> "以太网"
            else -> "其他网络"
        }
        return "网络：$transport\n目标：${targetName() ?: "未发现电脑"}\n发现设备：${deviceCount()}\n状态：${status()}"
    }

    fun diagnosticsText(): String = buildString {
        appendLine("墨堤诊断（Android）")
        appendLine(networkDiagnostics())
        appendLine("音频参数：${audioConfigLabel()}")
        appendLine("目标链路：${selectedLink()}")
        appendLine("活跃链路：${activeLink()}")
        appendLine()
        appendLine("最近应用日志：")
        append(ExportableLogger.snapshot())
    }

    fun share() {
        val intent = Intent(Intent.ACTION_SEND).apply {
            type = "text/plain"
            putExtra(Intent.EXTRA_SUBJECT, "墨堤诊断信息")
            putExtra(Intent.EXTRA_TEXT, diagnosticsText())
        }
        context.startActivity(Intent.createChooser(intent, "导出诊断信息"))
    }

    fun audioConfigLabel(): String = AudioConfig.DEFAULT.let { "${it.sampleRate / 1000} kHz · ${it.bitrate / 1000} kbps" }
}
