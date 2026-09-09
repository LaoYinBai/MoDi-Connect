package com.modi.connect.ui.runtime

import com.modi.connect.links.LinkManager
import com.modi.connect.links.LinkParams
import com.modi.connect.session.DisconnectReason
import com.modi.connect.ui.model.LinkChoice
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job

class ConnectionCoordinator(
    private val links: LinkManager,
    scope: CoroutineScope,
    onStatus: (LinkSwitchStatus) -> Unit,
) {
    private val port = object : LinkSwitchPort {
        override val activeLinkType: Byte? get() = links.activeLinkType
        override val isStreaming: Boolean get() = links.isStreaming
        override suspend fun notifyDisconnect(targetLink: Byte, reason: DisconnectReason) =
            links.notifyDisconnect(targetLink, reason)
        override fun cancelPendingConnection() = links.cancelPendingConnection()
        override suspend fun disconnectActive() = links.disconnectActive()
        override suspend fun connect(linkType: Byte, params: LinkParams) = links.connect(linkType, params)
    }
    private val switches = LinkSwitchCoordinator(port, scope, onStatus)

    suspend fun disconnectForSelection(choice: LinkChoice, reason: DisconnectReason = DisconnectReason.USER_SWITCH, forceCurrent: Boolean = false) =
        switches.disconnectForSelection(choice, reason, forceCurrent)
    fun select(choice: LinkChoice, params: LinkParams): Job = switches.select(choice, params)
    fun cancel() = switches.cancel()
}
