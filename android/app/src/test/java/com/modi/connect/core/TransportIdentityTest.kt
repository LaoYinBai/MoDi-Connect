package com.modi.connect.core

import org.junit.Assert.assertEquals
import org.junit.Test

class TransportIdentityTest {
    @Test
    fun stable_transport_identity_matches_windows_contract() {
        assertEquals(12345, TransportIdentity.AUDIO_PORT)
        assertEquals(12347, TransportIdentity.HANDSHAKE_PORT)
        assertEquals("_modi._udp", TransportIdentity.MDNS_SERVICE_TYPE)
        assertEquals("6d6f4469-0001-4000-8000-000000000001", TransportIdentity.BLUETOOTH_SERVICE_UUID.toString())
    }
}
