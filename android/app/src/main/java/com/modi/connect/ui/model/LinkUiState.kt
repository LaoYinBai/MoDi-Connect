package com.modi.connect.ui.model

import com.modi.connect.ConnectionState
import com.modi.protocol.LinkType

enum class LinkChoice(
    val linkType: Byte,
    val title: String,
    val environment: String
) {
    HOME(LinkType.WIFI_LAN, "在家", "同一路由器"),
    UNIVERSAL(LinkType.WIFI_DIRECT, "万能", "无路由器直连"),
    BLUETOOTH(LinkType.BLUETOOTH, "蓝牙", "近距离应急"),
    USB(LinkType.USB, "USB", "有线稳定");

    companion object {
        fun fromLinkType(linkType: Byte): LinkChoice? = entries.firstOrNull { it.linkType == linkType }
    }
}

enum class LinkRuntimePermission {
    NEARBY_WIFI_DEVICES,
    FINE_LOCATION,
    BLUETOOTH_CONNECT
}

fun LinkChoice.runtimePermission(apiLevel: Int): LinkRuntimePermission? = when (this) {
    LinkChoice.UNIVERSAL -> if (apiLevel >= 33) {
        LinkRuntimePermission.NEARBY_WIFI_DEVICES
    } else {
        LinkRuntimePermission.FINE_LOCATION
    }
    LinkChoice.BLUETOOTH -> if (apiLevel >= 31) LinkRuntimePermission.BLUETOOTH_CONNECT else null
    LinkChoice.HOME, LinkChoice.USB -> null
}

data class LinkUiState(
    val selected: LinkChoice = LinkChoice.HOME,
    val active: LinkChoice? = null,
    val connectionState: ConnectionState = ConnectionState.SEARCHING,
    val switching: Boolean = false,
    val hasP2pPairing: Boolean = false,
    val statusMessage: String = "正在寻找电脑"
) {
    val showScanButton: Boolean
        get() = selected == LinkChoice.UNIVERSAL

    val waitingMessage: String
        get() = when (selected) {
            LinkChoice.HOME -> "正在寻找电脑"
            LinkChoice.UNIVERSAL -> if (hasP2pPairing) "等待万能链路连接" else "等待扫码配对"
            LinkChoice.BLUETOOTH -> "等待已配对蓝牙电脑"
            LinkChoice.USB -> "等待 USB 连接"
        }

    val activeLabel: String
        get() = active?.let { "当前链路 ${it.title}" } ?: "当前无活跃链路"

    fun isSelected(choice: LinkChoice): Boolean = selected == choice
}
