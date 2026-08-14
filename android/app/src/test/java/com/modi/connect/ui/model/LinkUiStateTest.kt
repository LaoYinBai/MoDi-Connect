package com.modi.connect.ui.model

import com.modi.connect.ConnectionState
import com.modi.protocol.LinkType
import com.modi.connect.ui.link.parseMoDiQr
import com.modi.connect.ui.link.linkSelectionMenuItems
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class LinkUiStateTest {
    @Test
    fun `four choices preserve protocol id title and environment`() {
        assertEquals(
            listOf(LinkType.WIFI_LAN, LinkType.WIFI_DIRECT, LinkType.BLUETOOTH, LinkType.USB),
            LinkChoice.entries.map { it.linkType }
        )
        assertEquals(listOf("在家", "万能", "蓝牙", "USB"), LinkChoice.entries.map { it.title })
        assertEquals(
            listOf("同一路由器", "无路由器直连", "近距离应急", "有线稳定"),
            LinkChoice.entries.map { it.environment }
        )
    }

    @Test
    fun `selector menu keeps the approved four item order`() {
        assertEquals(
            listOf(LinkChoice.HOME, LinkChoice.UNIVERSAL, LinkChoice.BLUETOOTH, LinkChoice.USB),
            linkSelectionMenuItems()
        )
    }

    @Test
    fun `only universal mode exposes scan action`() {
        assertFalse(LinkUiState(selected = LinkChoice.HOME).showScanButton)
        assertTrue(LinkUiState(selected = LinkChoice.UNIVERSAL).showScanButton)
        assertFalse(LinkUiState(selected = LinkChoice.BLUETOOTH).showScanButton)
        assertFalse(LinkUiState(selected = LinkChoice.USB).showScanButton)
    }

    @Test
    fun `selected target and active transport stay independent`() {
        val state = LinkUiState(
            selected = LinkChoice.USB,
            active = LinkChoice.HOME,
            switching = true
        )

        assertEquals(LinkChoice.USB, state.selected)
        assertEquals(LinkChoice.HOME, state.active)
        assertTrue(state.switching)
        assertTrue(state.isSelected(LinkChoice.USB))
        assertFalse(state.isSelected(LinkChoice.HOME))
    }

    @Test
    fun `waiting messages describe the selected transport precondition`() {
        assertEquals("正在寻找电脑", LinkUiState(selected = LinkChoice.HOME).waitingMessage)
        assertEquals(
            "等待扫码配对",
            LinkUiState(selected = LinkChoice.UNIVERSAL, hasP2pPairing = false).waitingMessage
        )
        assertEquals(
            "等待万能链路连接",
            LinkUiState(selected = LinkChoice.UNIVERSAL, hasP2pPairing = true).waitingMessage
        )
        assertEquals("等待已配对蓝牙电脑", LinkUiState(selected = LinkChoice.BLUETOOTH).waitingMessage)
        assertEquals("等待 USB 连接", LinkUiState(selected = LinkChoice.USB).waitingMessage)
    }

    @Test
    fun `idle state never invents an active link`() {
        val state = LinkUiState(connectionState = ConnectionState.DISCONNECTED)

        assertNull(state.active)
        assertEquals("当前无活跃链路", state.activeLabel)
    }

    @Test
    fun `link permissions follow Android platform boundaries`() {
        assertEquals(null, LinkChoice.HOME.runtimePermission(apiLevel = 36))
        assertEquals(LinkRuntimePermission.NEARBY_WIFI_DEVICES, LinkChoice.UNIVERSAL.runtimePermission(33))
        assertEquals(LinkRuntimePermission.FINE_LOCATION, LinkChoice.UNIVERSAL.runtimePermission(32))
        assertEquals(LinkRuntimePermission.BLUETOOTH_CONNECT, LinkChoice.BLUETOOTH.runtimePermission(31))
        assertEquals(null, LinkChoice.BLUETOOTH.runtimePermission(apiLevel = 30))
        assertEquals(null, LinkChoice.USB.runtimePermission(apiLevel = 36))
    }

    @Test
    fun `scanner accepts only a valid wifi direct pairing payload`() {
        val parsed = parseMoDiQr(
            "MODI://version=1&transport=wifidirect&device=PC&token=12345678"
        )

        assertEquals("PC", parsed?.deviceName)
        assertEquals("12345678", parsed?.token)
        assertNull(parseMoDiQr("MODI://version=1&transport=usb&device=PC&token=12345678"))
        assertNull(parseMoDiQr("MODI://version=1&transport=wifidirect&device=PC&token="))
        assertNull(parseMoDiQr("https://example.com"))
    }
}
