package com.modi.connect.ui.runtime

import androidx.compose.runtime.mutableStateListOf
import com.modi.connect.ui.model.LanDevicePanelState
import com.modi.connect.ui.model.LanDeviceUiModel

class DiscoveryCoordinator {
    private val mutableDevices = mutableStateListOf<LanDeviceUiModel>()
    val devices: List<LanDeviceUiModel> get() = mutableDevices
    var selectedDevice: LanDeviceUiModel? = null
        private set

    fun found(name: String, host: String, port: Int): LanDeviceUiModel {
        val device = LanDeviceUiModel(name, host, port)
        val index = mutableDevices.indexOfFirst { it.endpointId == device.endpointId }
        if (index >= 0) mutableDevices[index] = device else mutableDevices.add(device)
        if (selectedDevice == null) selectedDevice = device
        return device
    }

    fun lost(name: String, host: String, port: Int) {
        val endpointId = LanDeviceUiModel(name, host, port).endpointId
        mutableDevices.removeAll { it.endpointId == endpointId }
        if (selectedDevice?.endpointId == endpointId) selectedDevice = mutableDevices.firstOrNull()
    }

    fun select(device: LanDeviceUiModel) { selectedDevice = device }
    fun resetSelection() { selectedDevice = null }
    fun selectFirstAvailable() { selectedDevice = mutableDevices.firstOrNull() }

    fun panel(connectedDevice: LanDeviceUiModel? = null): LanDevicePanelState = LanDevicePanelState.from(
        selectedEndpointId = selectedDevice?.endpointId,
        connectedDevice = connectedDevice,
        discoveredDevices = mutableDevices,
    )
}
