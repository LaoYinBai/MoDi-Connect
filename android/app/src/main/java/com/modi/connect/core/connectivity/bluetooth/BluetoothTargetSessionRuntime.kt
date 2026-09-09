package com.modi.connect.core.connectivity.bluetooth

import com.modi.connect.core.connectivity.PeerId
import com.modi.connect.core.connectivity.TransportEndpoint
import com.modi.connect.core.connectivity.TransportKind
import com.modi.connect.core.connectivity.legacy.AuthenticatedLegacySessionRuntime
import com.modi.protocol.ITransport
import java.security.MessageDigest
import java.util.UUID

class BluetoothTargetSessionRuntime(sessionUuid: UUID, peerCredential: String, legacy: ITransport) :
    AuthenticatedLegacySessionRuntime(
        sessionUuid,
        privatePeerId(peerCredential),
        TransportKind.Bluetooth,
        TransportEndpoint.parse("rfcomm://bonded-peer"),
        legacy,
    )

object BluetoothTargetComposition {
    fun isEnabled(buildFlag: Boolean): Boolean = buildFlag
}

private fun privatePeerId(value: String): PeerId {
    require(value.isNotBlank()) { "A bonded Bluetooth peer identity is required" }
    val digest = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(Charsets.UTF_8))
        .joinToString("") { "%02x".format(it) }
    return PeerId.parse("bluetooth:$digest")
}
