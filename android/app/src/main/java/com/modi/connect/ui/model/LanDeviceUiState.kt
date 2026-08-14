package com.modi.connect.ui.model

data class LanDeviceUiModel(
    val name: String,
    val host: String,
    val port: Int,
) {
    val endpointId: String = "${host.trim().lowercase()}:$port"
    val displayName: String = name.ifBlank { host }
    val endpointLabel: String = "$host:$port"
}

data class LanDevicePanelState(
    val selectedEndpointId: String? = null,
    val connectedDevice: LanDeviceUiModel? = null,
    val discoveredDevices: List<LanDeviceUiModel> = emptyList(),
) {
    val visibleDevices: List<LanDeviceUiModel>
        get() = discoveredDevices.filterNot { it.endpointId == connectedDevice?.endpointId }

    fun showFor(choice: LinkChoice): Boolean = choice == LinkChoice.HOME

    fun isSelected(device: LanDeviceUiModel): Boolean = selectedEndpointId == device.endpointId

    companion object {
        private val stableOrder = compareBy<LanDeviceUiModel>(
            { it.displayName.lowercase() },
            { it.host },
            { it.port },
        )

        fun from(
            selectedEndpointId: String?,
            connectedDevice: LanDeviceUiModel?,
            discoveredDevices: Iterable<LanDeviceUiModel>,
        ): LanDevicePanelState = LanDevicePanelState(
            selectedEndpointId = selectedEndpointId,
            connectedDevice = connectedDevice,
            discoveredDevices = discoveredDevices
                .associateBy(LanDeviceUiModel::endpointId)
                .values
                .sortedWith(stableOrder),
        )
    }
}

fun removeDiscoveredEndpoint(
    devices: List<LanDeviceUiModel>,
    endpointId: String,
): List<LanDeviceUiModel> = devices.filterNot { it.endpointId == endpointId }
