package com.modi.connect.ui.model

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LanDeviceUiStateTest {
    @Test
    fun `endpoint identity distinguishes computers with the same name`() {
        val office = LanDeviceUiModel("办公室", "192.168.1.20", 12347)
        val sameName = LanDeviceUiModel("办公室", "192.168.1.21", 12347)

        assertEquals("192.168.1.20:12347", office.endpointId)
        assertEquals("192.168.1.21:12347", sameName.endpointId)
    }

    @Test
    fun `panel deduplicates sorts and excludes the connected endpoint`() {
        val office = LanDeviceUiModel("办公室", "192.168.1.20", 12347)
        val sameName = LanDeviceUiModel("办公室", "192.168.1.21", 12347)
        val alpha = LanDeviceUiModel("alpha", "192.168.1.30", 12347)
        val alphaUpper = LanDeviceUiModel("ALPHA", "192.168.1.29", 12347)

        val state = LanDevicePanelState.from(
            selectedEndpointId = sameName.endpointId,
            connectedDevice = office,
            discoveredDevices = listOf(sameName, office, sameName, alpha, alphaUpper),
        )

        assertEquals(
            listOf(alphaUpper.endpointId, alpha.endpointId, sameName.endpointId),
            state.visibleDevices.map { it.endpointId },
        )
        assertTrue(state.isSelected(sameName))
        assertFalse(state.isSelected(office))
    }

    @Test
    fun `connected device survives outside the discovery snapshot`() {
        val connected = LanDeviceUiModel("直播电脑", "10.0.0.8", 12347)

        val state = LanDevicePanelState.from(
            selectedEndpointId = connected.endpointId,
            connectedDevice = connected,
            discoveredDevices = emptyList(),
        )

        assertEquals(connected, state.connectedDevice)
        assertTrue(state.visibleDevices.isEmpty())
    }

    @Test
    fun `LAN is the only link choice that shows the device panel`() {
        val state = LanDevicePanelState()

        assertTrue(state.showFor(LinkChoice.HOME))
        assertFalse(state.showFor(LinkChoice.UNIVERSAL))
        assertFalse(state.showFor(LinkChoice.BLUETOOTH))
        assertFalse(state.showFor(LinkChoice.USB))
    }

    @Test
    fun `lost endpoint removes only the matching same name computer`() {
        val remaining = removeDiscoveredEndpoint(
            listOf(
                LanDeviceUiModel("同名", "10.0.0.2", 12347),
                LanDeviceUiModel("同名", "10.0.0.3", 12347),
            ),
            endpointId = "10.0.0.2:12347",
        )

        assertEquals(listOf("10.0.0.3:12347"), remaining.map { it.endpointId })
    }
}
