package com.modi.connect.core.connectivity.usb

import com.modi.connect.core.connectivity.PeerId
import com.modi.connect.core.connectivity.TransportEndpoint
import com.modi.connect.core.connectivity.TransportKind
import com.modi.connect.core.connectivity.legacy.AuthenticatedLegacySessionRuntime
import com.modi.protocol.ITransport
import java.util.UUID

class UsbTargetSessionRuntime(sessionUuid: UUID, legacy: ITransport) :
    AuthenticatedLegacySessionRuntime(
        sessionUuid,
        PeerId.parse("usb-session:${sessionUuid.toString().replace("-", "")}"),
        TransportKind.Usb,
        TransportEndpoint.parse("adb://private-loopback-tunnel"),
        legacy,
    )

object UsbTargetComposition {
    fun isEnabled(buildFlag: Boolean): Boolean = buildFlag
}
