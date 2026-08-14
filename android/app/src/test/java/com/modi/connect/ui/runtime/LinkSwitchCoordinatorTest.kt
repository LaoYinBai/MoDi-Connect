package com.modi.connect.ui.runtime

import com.modi.connect.links.LinkParams
import com.modi.connect.session.DisconnectReason
import com.modi.connect.ui.model.LinkChoice
import com.modi.protocol.LinkType
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.cancel
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LinkSwitchCoordinatorTest {
    @Test
    fun `switch notifies old link before local disconnect`() = runBlocking {
        val events = mutableListOf<String>()
        val port = FakeLinkSwitchPort(events, active = LinkType.WIFI_LAN, isStreaming = true)
        val coordinator = LinkSwitchCoordinator(port, this) { }

        coordinator.select(LinkChoice.USB, LinkParams(route = 2)).join()

        assertEquals(listOf("notify:1->4", "disconnect:1", "connect:4:2"), events)
    }

    @Test
    fun `switch continues immediately when disconnect notification cannot be sent`() = runBlocking {
        val events = mutableListOf<String>()
        val port = FakeLinkSwitchPort(events, active = LinkType.BLUETOOTH, notifyResult = false)
        val coordinator = LinkSwitchCoordinator(port, this) { }

        coordinator.select(LinkChoice.HOME, LinkParams()).join()

        assertEquals(listOf("notify:3->1", "disconnect:3", "connect:1:0"), events)
        assertEquals(LinkType.WIFI_LAN, port.activeLinkType)
    }

    @Test
    fun `selecting the active link is a no op`() = runBlocking {
        val events = mutableListOf<String>()
        val port = FakeLinkSwitchPort(events, active = LinkType.USB, isStreaming = true)
        val coordinator = LinkSwitchCoordinator(port, this) { }

        coordinator.select(LinkChoice.USB, LinkParams()).join()

        assertTrue(events.isEmpty())
    }

    @Test
    fun `switching LAN peer notifies and disconnects the old LAN session`() = runBlocking {
        val events = mutableListOf<String>()
        val port = FakeLinkSwitchPort(events, active = LinkType.WIFI_LAN, isStreaming = true)
        val coordinator = LinkSwitchCoordinator(port, this) { }

        coordinator.select(
            LinkChoice.HOME,
            LinkParams(host = "10.0.0.3", route = 1),
            forceReconnectCurrent = true,
        ).join()

        assertEquals(listOf("notify:1->1", "disconnect:1", "connect:1:1"), events)
    }

    @Test
    fun `retrying an inactive selected link reconnects it`() = runBlocking {
        val events = mutableListOf<String>()
        val port = FakeLinkSwitchPort(events, active = LinkType.USB, isStreaming = false)
        val coordinator = LinkSwitchCoordinator(port, this) { }

        coordinator.select(LinkChoice.USB, LinkParams(route = 1)).join()

        assertEquals(listOf("connect:4:1"), events)
    }

    @Test
    fun `only the latest switch generation may publish completion`() = runBlocking {
        val events = mutableListOf<String>()
        val updates = mutableListOf<LinkSwitchStatus>()
        val usbGate = CompletableDeferred<Unit>()
        val port = FakeLinkSwitchPort(events, active = LinkType.WIFI_LAN, usbGate = usbGate)
        val coordinator = LinkSwitchCoordinator(port, this, updates::add)

        val usb = coordinator.select(LinkChoice.USB, LinkParams(route = 3))
        while ("connect:4:3" !in events) kotlinx.coroutines.yield()
        val bluetooth = coordinator.select(LinkChoice.BLUETOOTH, LinkParams(route = 3))
        bluetooth.join()
        usb.join()

        assertFalse(updates.any { it.selected == LinkChoice.USB && !it.switching && it.connected })
        assertTrue(events.indexOf("cancel:4") < events.indexOf("connect:3:3"))
        assertEquals(LinkChoice.BLUETOOTH, updates.last().selected)
        assertTrue(updates.last().connected)
    }

    @Test
    fun `only the latest LAN peer selection may finish connecting`() = runBlocking {
        val events = mutableListOf<String>()
        val updates = mutableListOf<LinkSwitchStatus>()
        val firstLanGate = CompletableDeferred<Unit>()
        val port = FakeLinkSwitchPort(
            events,
            active = LinkType.WIFI_LAN,
            isStreaming = true,
            firstLanGate = firstLanGate,
        )
        val coordinator = LinkSwitchCoordinator(port, this, updates::add)

        val first = coordinator.select(
            LinkChoice.HOME,
            LinkParams(host = "10.0.0.2", route = 2),
            forceReconnectCurrent = true,
        )
        while ("10.0.0.2" !in port.connectHosts) kotlinx.coroutines.yield()
        val latest = coordinator.select(
            LinkChoice.HOME,
            LinkParams(host = "10.0.0.3", route = 2),
            forceReconnectCurrent = true,
        )
        latest.join()
        first.join()

        assertEquals(listOf("10.0.0.2", "10.0.0.3"), port.connectHosts)
        assertEquals(1, updates.count { !it.switching && it.connected })
        assertTrue(updates.last().connected)
    }

    @Test
    fun `switching while streaming preserves the requested route`() = runBlocking {
        val events = mutableListOf<String>()
        val port = FakeLinkSwitchPort(events, active = LinkType.WIFI_DIRECT, isStreaming = true)
        val coordinator = LinkSwitchCoordinator(port, this) { }

        coordinator.select(LinkChoice.BLUETOOTH, LinkParams(route = 3)).join()

        assertEquals("connect:3:3", events.last())
    }

    @Test
    fun `connection intents map all choices without LAN assumptions`() {
        assertEquals(
            LinkType.WIFI_LAN,
            buildLinkConnectionIntent(LinkChoice.HOME, route = 1, lanHost = "10.0.0.2", p2pPair = null).linkType
        )
        assertEquals(
            LinkType.WIFI_DIRECT,
            buildLinkConnectionIntent(LinkChoice.UNIVERSAL, route = 1, lanHost = null, p2pPair = "TOKEN" to "PC").linkType
        )
        assertEquals(
            LinkType.BLUETOOTH,
            buildLinkConnectionIntent(LinkChoice.BLUETOOTH, route = 1, lanHost = null, p2pPair = null).linkType
        )
        assertEquals(
            LinkType.USB,
            buildLinkConnectionIntent(LinkChoice.USB, route = 1, lanHost = null, p2pPair = null).linkType
        )
    }

    @Test
    fun `connection intent reports only the selected link precondition`() {
        val home = buildLinkConnectionIntent(LinkChoice.HOME, 0, lanHost = null, p2pPair = null)
        val universal = buildLinkConnectionIntent(LinkChoice.UNIVERSAL, 0, lanHost = null, p2pPair = null)
        val bluetooth = buildLinkConnectionIntent(LinkChoice.BLUETOOTH, 0, lanHost = null, p2pPair = null)
        val usb = buildLinkConnectionIntent(LinkChoice.USB, 0, lanHost = null, p2pPair = null)

        assertEquals("未发现电脑，请先启动电脑端", home.blockingReason)
        assertEquals("等待扫码配对", universal.blockingReason)
        assertEquals(null, bluetooth.blockingReason)
        assertEquals(null, usb.blockingReason)
        assertEquals(null, bluetooth.params.host)
        assertEquals(null, usb.params.host)
    }

    private class FakeLinkSwitchPort(
        private val events: MutableList<String>,
        active: Byte?,
        override val isStreaming: Boolean = false,
        private val notifyResult: Boolean = true,
        private val usbGate: CompletableDeferred<Unit>? = null,
        private val firstLanGate: CompletableDeferred<Unit>? = null,
    ) : LinkSwitchPort {
        override var activeLinkType: Byte? = active
        val connectHosts = mutableListOf<String>()

        override suspend fun notifyDisconnect(targetLink: Byte, reason: DisconnectReason): Boolean {
            events += "notify:${activeLinkType?.toInt()}->${targetLink.toInt()}"
            return notifyResult
        }

        override fun cancelPendingConnection() {
            events += "cancel-pending"
        }

        override fun disconnectActive() {
            events += "disconnect:${activeLinkType?.toInt()}"
            activeLinkType = null
        }

        override suspend fun connect(linkType: Byte, params: LinkParams): Boolean {
            events += "connect:${linkType.toInt()}:${params.route}"
            params.host?.let(connectHosts::add)
            if (linkType == LinkType.WIFI_LAN && params.host == "10.0.0.2" && firstLanGate != null) {
                firstLanGate.await()
            }
            if (linkType == LinkType.USB && usbGate != null) {
                try {
                    usbGate.await()
                } finally {
                    events += "cancel:${linkType.toInt()}"
                }
            }
            activeLinkType = linkType
            return true
        }
    }
}
