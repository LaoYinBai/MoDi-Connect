package com.modi.connect.ui.runtime

import com.modi.connect.links.LinkParams
import com.modi.connect.session.DisconnectReason
import com.modi.connect.ui.model.LinkChoice
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.launch

data class LinkConnectionIntent(
    val linkType: Byte,
    val params: LinkParams,
    val blockingReason: String? = null
)

fun buildLinkConnectionIntent(
    choice: LinkChoice,
    route: Int,
    lanHost: String?,
    p2pPair: Pair<String, String>?
): LinkConnectionIntent = when (choice) {
    LinkChoice.HOME -> LinkConnectionIntent(
        choice.linkType,
        LinkParams(host = lanHost, route = route),
        blockingReason = if (lanHost == null) "未发现电脑，请先启动电脑端" else null
    )
    LinkChoice.UNIVERSAL -> LinkConnectionIntent(
        choice.linkType,
        LinkParams(token = p2pPair?.first, deviceName = p2pPair?.second, route = route),
        blockingReason = if (p2pPair == null) "等待扫码配对" else null
    )
    LinkChoice.BLUETOOTH, LinkChoice.USB -> LinkConnectionIntent(
        choice.linkType,
        LinkParams(route = route)
    )
}

interface LinkSwitchPort {
    val activeLinkType: Byte?
    val isStreaming: Boolean
    suspend fun notifyDisconnect(targetLink: Byte, reason: DisconnectReason): Boolean
    fun cancelPendingConnection()
    suspend fun disconnectActive()
    suspend fun connect(linkType: Byte, params: LinkParams): Boolean
}

data class LinkSwitchStatus(
    val selected: LinkChoice,
    val switching: Boolean,
    val connected: Boolean,
    val message: String
)

class LinkSwitchCoordinator(
    private val port: LinkSwitchPort,
    private val scope: CoroutineScope,
    private val onStatus: (LinkSwitchStatus) -> Unit
) {
    private var switchJob: Job? = null
    private var switchGeneration = 0L

    suspend fun disconnectForSelection(
        choice: LinkChoice,
        reason: DisconnectReason = DisconnectReason.USER_SWITCH,
        forceCurrent: Boolean = false
    ) {
        val previousJob = switchJob
        if (previousJob?.isActive == true) {
            previousJob.cancel()
            port.cancelPendingConnection()
            previousJob.join()
        }
        if (switchJob === previousJob) switchJob = null
        switchGeneration++
        val previous = port.activeLinkType
        if (previous != null && (forceCurrent || previous != choice.linkType)) {
            onStatus(LinkSwitchStatus(choice, switching = true, connected = false, message = "正在切换到${choice.title}"))
            port.notifyDisconnect(choice.linkType, reason)
            port.disconnectActive()
        }
    }

    fun select(
        choice: LinkChoice,
        params: LinkParams,
        forceReconnectCurrent: Boolean = false,
    ): Job {
        if (!forceReconnectCurrent &&
            port.activeLinkType == choice.linkType && port.isStreaming && switchJob?.isActive != true
        ) {
            return scope.launch { }
        }

        val previousJob = switchJob
        if (previousJob?.isActive == true) {
            previousJob.cancel()
            port.cancelPendingConnection()
        }
        val generation = ++switchGeneration
        val wasStreaming = port.isStreaming
        onStatus(LinkSwitchStatus(choice, switching = true, connected = false, message = "正在切换到${choice.title}"))

        return scope.launch {
            previousJob?.join()
            val previous = port.activeLinkType
            if (previous != null && (previous != choice.linkType || forceReconnectCurrent)) {
                port.notifyDisconnect(choice.linkType, DisconnectReason.USER_SWITCH)
                port.disconnectActive()
            }

            val connected = port.connect(choice.linkType, params)
            if (generation != switchGeneration) return@launch

            onStatus(
                LinkSwitchStatus(
                    selected = choice,
                    switching = false,
                    connected = connected,
                    message = when {
                        connected && wasStreaming -> "已通过${choice.title}恢复推流"
                        connected -> "${choice.title}链路已连接"
                        else -> "等待${choice.title}链路连接"
                    }
                )
            )
        }.also { switchJob = it }
    }

    fun cancel() {
        switchGeneration++
        switchJob?.cancel()
        switchJob = null
    }
}
