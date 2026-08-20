package com.modi.connect.core

import java.util.UUID

object TransportIdentity {
    const val AUDIO_PORT = 12345
    const val HANDSHAKE_PORT = 12347
    const val MDNS_SERVICE_TYPE = "_modi._udp"
    val BLUETOOTH_SERVICE_UUID: UUID = UUID.fromString("6D6F4469-0001-4000-8000-000000000001")
}
