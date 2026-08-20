package com.modi.connect.ui.model

import com.modi.connect.ConnectionState

data class PipelineOption(
    val route: Int,
    val title: String,
    val direction: String,
    val requiresMediaProjection: Boolean,
    val requiresMicrophone: Boolean
) {
    companion object {
        fun defaults(): List<PipelineOption> = listOf(
            PipelineOption(0, "扬声器", "系统音频 → 电脑扬声器", requiresMediaProjection = true, requiresMicrophone = false),
            PipelineOption(1, "混音监听", "系统音频 + 麦克风 → 扬声器", requiresMediaProjection = true, requiresMicrophone = true),
            PipelineOption(2, "虚拟麦克风", "麦克风 → 电脑输入", requiresMediaProjection = false, requiresMicrophone = true),
            PipelineOption(3, "整活通道", "系统音频 → 电脑输入", requiresMediaProjection = true, requiresMicrophone = false)
        )
    }
}

enum class PermissionRequirement {
    MICROPHONE,
    MEDIA_PROJECTION,
    READY
}

fun PipelineOption.nextPermission(
    hasMicrophonePermission: Boolean,
    hasMediaProjection: Boolean
): PermissionRequirement = when {
    requiresMicrophone && !hasMicrophonePermission -> PermissionRequirement.MICROPHONE
    requiresMediaProjection && !hasMediaProjection -> PermissionRequirement.MEDIA_PROJECTION
    else -> PermissionRequirement.READY
}

enum class StreamButtonState {
    IDLE,
    PERMISSION_REQUESTING,
    CONNECTING,
    STREAMING,
    ERROR
}

fun StreamButtonState.acceptsStartTap(): Boolean =
    this == StreamButtonState.IDLE || this == StreamButtonState.ERROR

data class AudioUiState(
    val pipelines: List<PipelineOption> = PipelineOption.defaults(),
    val selectedRoute: Int = 0,
    val streamButtonState: StreamButtonState = StreamButtonState.IDLE,
    val longPressProgress: Float = 0f,
    val audioLevel: Float = 0f,
    val streamVolume: Float = 1f,
    val showVolumeHud: Boolean = false,
    val statusMessage: String = "正在寻找电脑",
    val targetDeviceName: String? = null,
    val lanDevices: LanDevicePanelState = LanDevicePanelState(),
    val link: LinkUiState = LinkUiState()
)

fun ConnectionState.toStreamButtonState(): StreamButtonState = when (this) {
    ConnectionState.IDLE,
    ConnectionState.DISCONNECTED,
    ConnectionState.SEARCHING,
    ConnectionState.FOUND -> StreamButtonState.IDLE
    ConnectionState.CONNECTING,
    ConnectionState.CONNECTED,
    ConnectionState.RECONNECTING -> StreamButtonState.CONNECTING
    ConnectionState.STREAMING -> StreamButtonState.STREAMING
    ConnectionState.ERROR -> StreamButtonState.ERROR
}
