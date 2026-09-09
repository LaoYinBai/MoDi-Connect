package com.modi.connect.ui.runtime

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class DiscoveryCoordinatorTest {
    @Test
    fun `discovery deduplicates endpoints and preserves explicit selection`() {
        val coordinator = DiscoveryCoordinator()
        coordinator.found("电脑 A", "192.168.1.2", 12345)
        coordinator.found("电脑 A 新名称", "192.168.1.2", 12345)
        coordinator.found("电脑 B", "192.168.1.3", 12345)

        assertEquals(2, coordinator.devices.size)
        assertEquals("192.168.1.2:12345", coordinator.selectedDevice?.endpointId)
        coordinator.select(coordinator.devices.last())
        coordinator.lost("电脑 A", "192.168.1.2", 12345)

        assertEquals("192.168.1.3:12345", coordinator.selectedDevice?.endpointId)
        assertEquals(1, coordinator.panel().discoveredDevices.size)
        coordinator.resetSelection()
        assertNull(coordinator.selectedDevice)
    }
}
