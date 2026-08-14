package com.modi.connect.links

import com.modi.connect.core.adapters.BluetoothTransport
import com.modi.connect.core.adapters.UdpTransport
import com.modi.connect.core.adapters.UsbTransport
import com.modi.connect.core.factory.PlatformFactory
import com.modi.protocol.LinkType
import com.modi.protocol.Packet
import com.modi.protocol.PacketHeaderCodec
import com.modi.protocol.PacketType
import com.modi.protocol.SequenceHelper
import com.modi.protocol.TransportType
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class FourLinkProtocolRegressionTest {
    @Test
    fun lanUsesPinnedProtocolWithoutFallback() = assertLink(
        "LAN", LinkType.WIFI_LAN, TransportType.Udp
    )

    @Test
    fun wifiDirectUsesPinnedProtocolWithoutFallback() = assertLink(
        "Wi-Fi Direct", LinkType.WIFI_DIRECT, TransportType.Udp
    )

    @Test
    fun bluetoothUsesPinnedProtocolWithoutFallback() = assertLink(
        "Bluetooth", LinkType.BLUETOOTH, TransportType.Bluetooth
    )

    @Test
    fun usbUsesPinnedProtocolWithoutFallback() = assertLink(
        "USB", LinkType.USB, TransportType.Usb
    )

    private fun assertLink(name: String, linkType: Byte, expectedTransport: TransportType) {
        val codec = PlatformFactory.createProtocol()
        assertTrue(codec is PacketHeaderCodec)

        val hello = Packet(PacketType.HELLO, linkType, 0u, byteArrayOf(0x11, 0x22))
        val encodedHello = codec.encode(hello)
        assertEquals(linkType, encodedHello[6])
        assertEquals(17, encodedHello.size)
        assertEquals(2, encodedHello[14].toInt())
        assertPacket(codec.decode(encodedHello), PacketType.HELLO, linkType, 0u, byteArrayOf(0x11, 0x22))

        val ack = Packet(PacketType.HELLO_ACK, linkType, UInt.MAX_VALUE, byteArrayOf())
        val encodedAck = codec.encode(ack)
        assertPacket(codec.decode(encodedAck), PacketType.HELLO_ACK, linkType, UInt.MAX_VALUE, byteArrayOf())

        assertNull(codec.decode(encodedHello.copyOf(encodedHello.size - 1)))
        assertTrue(SequenceHelper.before(UInt.MAX_VALUE, 0u))
        assertEquals(1u, SequenceHelper.distance(UInt.MAX_VALUE, 0u))

        val actualTransport = when (linkType) {
            LinkType.WIFI_LAN, LinkType.WIFI_DIRECT -> UdpTransport(0).type
            LinkType.BLUETOOTH -> BluetoothTransport().type
            LinkType.USB -> UsbTransport().type
            else -> error("Unsupported manually selected link: $linkType")
        }
        assertEquals(expectedTransport, actualTransport)
        assertEquals(name, linkName(linkType))
    }

    private fun linkName(linkType: Byte): String = when (linkType) {
        LinkType.WIFI_LAN -> "LAN"
        LinkType.WIFI_DIRECT -> "Wi-Fi Direct"
        LinkType.BLUETOOTH -> "Bluetooth"
        LinkType.USB -> "USB"
        else -> error("Unsupported manually selected link: $linkType")
    }

    private fun assertPacket(
        actual: Packet?,
        type: PacketType,
        linkType: Byte,
        sequence: UInt,
        payload: ByteArray
    ) {
        requireNotNull(actual)
        assertEquals(type, actual.type)
        assertEquals(linkType, actual.linkType)
        assertEquals(sequence, actual.sequence)
        assertArrayEquals(payload, actual.payload)
    }
}
