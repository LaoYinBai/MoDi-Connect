package com.modi.connect.ui.runtime

import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjection
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import androidx.activity.ComponentActivity
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.modi.connect.ConnectionState
import com.modi.connect.ConnectionStateManager
import com.modi.connect.MediaProjectionService
import com.modi.connect.audio.AudioConfig
import com.modi.connect.audio.AudioPipeline
import com.modi.connect.audio.AndroidMuteRecovery
import com.modi.connect.audio.MediaProjectionOwner
import com.modi.connect.audio.StreamGain
import com.modi.connect.core.impl.ExportableLogger
import com.modi.connect.core.infrastructure.Log
import com.modi.connect.links.LinkManager
import com.modi.connect.links.LinkParams
import com.modi.connect.net.P2pPairStore
import com.modi.connect.ui.model.AudioUiState
import com.modi.connect.ui.model.LanDevicePanelState
import com.modi.connect.ui.model.LanDeviceUiModel
import com.modi.connect.ui.model.LinkChoice
import com.modi.connect.ui.model.LinkUiState
import com.modi.connect.ui.model.StreamButtonState
import com.modi.connect.ui.model.toStreamButtonState
import com.modi.connect.ui.link.MoDiQrCode
import com.modi.protocol.LinkType
import com.modi.connect.session.DisconnectReason
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlin.coroutines.coroutineContext

data class LinkStartRequest(
    val choice: LinkChoice,
    val generation: Long
)

class MoDiRuntime(private val activity: ComponentActivity) {
    private val mainScope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val projectionOwner = MediaProjectionOwner(mainScope) {
        stopStreaming()
        reportError("系统录音授权已结束，请重新授权")
    }
    private val stateManager = ConnectionStateManager()
    private val streamGainStore = StreamGainStore(activity, mainScope)
    private val pipeline = AudioPipeline().also { it.setStreamVolume(streamGainStore.read()) }
    val linkManager = LinkManager(activity, pipeline, stateManager)
    private val switchPort = object : LinkSwitchPort {
        override val activeLinkType: Byte? get() = linkManager.activeLinkType
        override val isStreaming: Boolean get() = linkManager.isStreaming
        override suspend fun notifyDisconnect(targetLink: Byte, reason: DisconnectReason): Boolean =
            linkManager.notifyDisconnect(targetLink, reason)
        override fun cancelPendingConnection() = linkManager.cancelPendingConnection()
        override suspend fun disconnectActive() = linkManager.disconnectActive()
        override suspend fun connect(linkType: Byte, params: LinkParams): Boolean =
            linkManager.connect(linkType, params)
    }
    private val switchCoordinator = LinkSwitchCoordinator(switchPort, mainScope, ::onSwitchStatus)
    private val operationMutex = Mutex()
    private val streamVolumeController = StreamVolumeController(mainScope)
    private val muteRecoveryJob: Job

    private val devices = mutableStateListOf<LanDeviceUiModel>()
    val discoveredDevices: List<LanDeviceUiModel> get() = devices

    var audioUiState by mutableStateOf(AudioUiState(streamVolume = pipeline.streamVolume()))
        private set

    val hasMediaProjection: Boolean get() = projectionOwner.hasProjection

    private var selectedLanDevice: LanDeviceUiModel? = null
    private var lastLevelUpdateNanos = 0L
    private var selectionPreparation: Job? = null
    private var resumeAfterSelection = false
    private var selectionGeneration = 0L

    init {
        Log.setImpl(ExportableLogger)
        muteRecoveryJob = mainScope.launch(Dispatchers.IO) {
            AndroidMuteRecovery.reconcileOnColdStart(activity)
        }
        stateManager.onStateChanged = { connectionState ->
            mainScope.launch {
                audioUiState = audioUiState.copy(
                    streamButtonState = connectionState.toStreamButtonState(),
                    statusMessage = stateManager.lastReason ?: connectionState.toChineseLabel(),
                    link = audioUiState.link.copy(
                        connectionState = connectionState,
                        active = when (connectionState) {
                            ConnectionState.CONNECTED, ConnectionState.STREAMING, ConnectionState.RECONNECTING ->
                                LinkChoice.fromLinkType(linkManager.activeLinkType ?: -1)
                            else -> null
                        },
                        switching = connectionState == ConnectionState.CONNECTING
                    ),
                    lanDevices = currentLanPanel(
                        connectedDevice = when (connectionState) {
                            ConnectionState.IDLE,
                            ConnectionState.DISCONNECTED,
                            ConnectionState.ERROR -> null
                            else -> audioUiState.lanDevices.connectedDevice
                        },
                    ),
                )
            }
        }

        listOf(
            LinkChoice.HOME to linkManager.wifiLan,
            LinkChoice.UNIVERSAL to linkManager.wifiDirect,
            LinkChoice.BLUETOOTH to linkManager.bluetooth,
            LinkChoice.USB to linkManager.usb
        ).forEach { (choice, link) ->
            link.onStatusChanged = { message ->
                mainScope.launch {
                    if (audioUiState.link.selected == choice || audioUiState.link.active == choice) {
                        audioUiState = audioUiState.copy(
                            statusMessage = message,
                            link = audioUiState.link.copy(statusMessage = message)
                        )
                    }
                }
            }
            link.onStreamingChanged = { streaming ->
                mainScope.launch {
                    if (audioUiState.link.selected == choice || audioUiState.link.active == choice) {
                        audioUiState = audioUiState.copy(
                            streamButtonState = if (streaming) StreamButtonState.STREAMING else stateManager.state.toStreamButtonState(),
                            audioLevel = if (streaming) audioUiState.audioLevel else 0f,
                            link = audioUiState.link.copy(
                                active = when {
                                    streaming -> choice
                                    audioUiState.link.active == choice -> null
                                    else -> audioUiState.link.active
                                },
                                connectionState = stateManager.state,
                                switching = false
                            ),
                            lanDevices = currentLanPanel(
                                connectedDevice = when {
                                    choice != LinkChoice.HOME -> audioUiState.lanDevices.connectedDevice
                                    streaming -> selectedLanDevice
                                    else -> null
                                },
                            ),
                        )
                        if (streaming) {
                            delay(300)
                            stopProjectionPreparationService()
                        }
                    }
                }
            }
        }

        linkManager.wifiLan.onDeviceFound = { device ->
            mainScope.launch {
                val uiDevice = LanDeviceUiModel(device.name, device.host, device.port)
                val index = devices.indexOfFirst { it.endpointId == uiDevice.endpointId }
                if (index >= 0) devices[index] = uiDevice else devices.add(uiDevice)
                if (selectedLanDevice == null) {
                    selectedLanDevice = uiDevice
                    audioUiState = audioUiState.copy(
                        targetDeviceName = uiDevice.displayName,
                        statusMessage = if (audioUiState.link.selected == LinkChoice.HOME) {
                            "已发现 ${uiDevice.displayName}"
                        } else {
                            audioUiState.statusMessage
                        },
                    )
                }
                audioUiState = audioUiState.copy(lanDevices = currentLanPanel())
            }
        }
        linkManager.wifiLan.onDeviceLost = { lostDevice ->
            mainScope.launch {
                val endpointId = LanDeviceUiModel(
                    lostDevice.name,
                    lostDevice.host,
                    lostDevice.port,
                ).endpointId
                devices.removeAll { it.endpointId == endpointId }
                audioUiState = audioUiState.copy(lanDevices = currentLanPanel())
            }
        }

        pipeline.onAudioLevel = { level ->
            val now = System.nanoTime()
            if (now - lastLevelUpdateNanos >= 33_000_000L) {
                lastLevelUpdateNanos = now
                mainScope.launch { audioUiState = audioUiState.copy(audioLevel = level) }
            }
        }
    }

    fun start() {
        if (stateManager.state == ConnectionState.IDLE || stateManager.state == ConnectionState.DISCONNECTED) {
            stateManager.update(ConnectionState.SEARCHING)
        }
        linkManager.wifiLan.start()
        audioUiState = audioUiState.copy(
            link = audioUiState.link.copy(hasP2pPairing = P2pPairStore.hasPaired(activity))
        )
    }

    fun close() {
        streamVolumeController.cancel()
        selectionPreparation?.cancel()
        selectionPreparation = null
        pipeline.onAudioLevel = null
        stateManager.onStateChanged = null
        projectionOwner.clear(stopProjection = true)
        stopProjectionPreparationService()
        mainScope.launch {
            linkManager.disconnect()
            linkManager.wifiLan.stop()
        }.invokeOnCompletion { mainScope.cancel() }
    }

    fun currentStartRequest(): LinkStartRequest =
        LinkStartRequest(audioUiState.link.selected, selectionGeneration)

    suspend fun selectLink(choice: LinkChoice): LinkStartRequest? {
        if (audioUiState.link.selected == choice) return null
        selectionGeneration++
        val shouldResume = resumeAfterSelection || linkManager.isStreaming ||
            audioUiState.streamButtonState in setOf(
                StreamButtonState.PERMISSION_REQUESTING,
                StreamButtonState.CONNECTING,
                StreamButtonState.STREAMING
            )
        resumeAfterSelection = shouldResume
        val wasSwitching = audioUiState.link.switching
        val activeLinkType = linkManager.activeLinkType
        val shouldCancelOrDisconnect = wasSwitching ||
            activeLinkType != null && activeLinkType != choice.linkType
        audioUiState = audioUiState.copy(
            link = audioUiState.link.copy(
                selected = choice,
                switching = shouldCancelOrDisconnect,
                hasP2pPairing = P2pPairStore.hasPaired(activity),
                statusMessage = LinkUiState(selected = choice, hasP2pPairing = P2pPairStore.hasPaired(activity)).waitingMessage
            ),
            statusMessage = LinkUiState(selected = choice, hasP2pPairing = P2pPairStore.hasPaired(activity)).waitingMessage
        )
        val request = currentStartRequest()
        if (shouldCancelOrDisconnect) {
            val currentJob = coroutineContext[Job]
            selectionPreparation?.takeIf { it !== currentJob }?.cancel()
            selectionPreparation = currentJob
            try {
                switchCoordinator.disconnectForSelection(choice)
            } finally {
                if (selectionPreparation === currentJob) selectionPreparation = null
            }
        }
        return if (shouldResume && request == currentStartRequest()) request else null
    }

    suspend fun selectLanDevice(device: LanDeviceUiModel): LinkStartRequest? {
        if (audioUiState.link.selected != LinkChoice.HOME) return null
        if (audioUiState.lanDevices.connectedDevice?.endpointId == device.endpointId) return null
        if (selectedLanDevice?.endpointId == device.endpointId) return null

        selectionGeneration++
        val shouldResume = resumeAfterSelection || linkManager.isStreaming ||
            audioUiState.streamButtonState in setOf(
                StreamButtonState.PERMISSION_REQUESTING,
                StreamButtonState.CONNECTING,
                StreamButtonState.STREAMING,
            )
        resumeAfterSelection = shouldResume
        selectedLanDevice = device
        audioUiState = audioUiState.copy(
            targetDeviceName = device.displayName,
            statusMessage = "已选择 ${device.displayName}",
            link = audioUiState.link.copy(
                switching = shouldResume,
                statusMessage = "已选择 ${device.displayName}",
            ),
            lanDevices = currentLanPanel(),
        )
        val request = currentStartRequest()
        if (!shouldResume) return null

        val currentJob = coroutineContext[Job]
        selectionPreparation?.takeIf { it !== currentJob }?.cancel()
        selectionPreparation = currentJob
        try {
            switchCoordinator.disconnectForSelection(
                LinkChoice.HOME,
                forceCurrent = true,
            )
        } finally {
            if (selectionPreparation === currentJob) selectionPreparation = null
        }
        return request.takeIf { it == currentStartRequest() }
    }

    fun setMediaProjection(projection: MediaProjection?) {
        projectionOwner.replace(projection)
    }

    fun setPermissionRequesting(requesting: Boolean) {
        val message = if (requesting) "正在请求系统授权" else audioUiState.statusMessage
        audioUiState = audioUiState.copy(
            streamButtonState = if (requesting) StreamButtonState.PERMISSION_REQUESTING else stateManager.state.toStreamButtonState(),
            statusMessage = message,
            link = audioUiState.link.copy(
                connectionState = if (requesting) ConnectionState.CONNECTING else stateManager.state,
                switching = requesting,
                statusMessage = message
            ),
            lanDevices = currentLanPanel(connectedDevice = null),
        )
    }

    fun reportError(message: String) {
        stopProjectionPreparationService()
        audioUiState = audioUiState.copy(
            streamButtonState = StreamButtonState.ERROR,
            statusMessage = message,
            audioLevel = 0f,
            link = audioUiState.link.copy(
                active = null,
                connectionState = ConnectionState.ERROR,
                switching = false,
                statusMessage = message
            ),
            lanDevices = currentLanPanel(connectedDevice = null),
        )
    }

    fun cancelPermissionRequest(message: String) {
        resumeAfterSelection = false
        audioUiState = audioUiState.copy(
            streamButtonState = StreamButtonState.ERROR,
            statusMessage = message,
            link = audioUiState.link.copy(
                active = null,
                connectionState = ConnectionState.ERROR,
                switching = false,
                statusMessage = message
            )
        )
    }

    fun selectPipeline(route: Int) {
        val option = audioUiState.pipelines.firstOrNull { it.route == route } ?: return
        audioUiState = audioUiState.copy(selectedRoute = option.route)
        if (linkManager.isStreaming) {
            mainScope.launch {
                muteRecoveryJob.join()
                operationMutex.withLock {
                    val updated = linkManager.sendRouteUpdate(option.route, projectionOwner.current())
                    audioUiState = audioUiState.copy(
                        statusMessage = if (updated) "已切换到${option.title}" else "切换失败，请检查权限"
                    )
                    stopProjectionPreparationService()
                }
            }
        }
    }

    fun requestStart(request: LinkStartRequest = currentStartRequest()) {
        mainScope.launch {
            muteRecoveryJob.join()
            val preparation = selectionPreparation
            preparation?.join()
            if (selectionPreparation === preparation) {
                selectionPreparation = null
            }
            if (request != currentStartRequest()) return@launch
            val intent = currentConnectionIntent()
            if (intent.blockingReason != null) {
                resumeAfterSelection = false
                if (audioUiState.link.selected == LinkChoice.UNIVERSAL) {
                    audioUiState = audioUiState.copy(
                        streamButtonState = StreamButtonState.IDLE,
                        statusMessage = intent.blockingReason,
                        link = audioUiState.link.copy(
                            active = null,
                            connectionState = stateManager.state,
                            statusMessage = intent.blockingReason,
                            switching = false
                        )
                    )
                } else {
                    reportError(intent.blockingReason)
                }
                return@launch
            }

            resumeAfterSelection = false
            stateManager.beginConnecting()
            val params = intent.params.copy(proj = projectionOwner.current())
            operationMutex.withLock {
                switchCoordinator.select(audioUiState.link.selected, params).join()
            }
        }
    }

    suspend fun applyP2pPair(qr: MoDiQrCode) {
        if (audioUiState.link.selected != LinkChoice.UNIVERSAL) return
        val shouldResume = resumeAfterSelection || linkManager.isStreaming ||
            audioUiState.streamButtonState in setOf(
                StreamButtonState.PERMISSION_REQUESTING,
                StreamButtonState.CONNECTING,
                StreamButtonState.STREAMING
            )
        resumeAfterSelection = shouldResume
        selectionGeneration++
        val startRequest = currentStartRequest()
        val currentJob = coroutineContext[Job]
        selectionPreparation?.takeIf { it !== currentJob }?.cancel()
        selectionPreparation = currentJob
        var restart = false
        try {
            switchCoordinator.disconnectForSelection(
                LinkChoice.UNIVERSAL,
                DisconnectReason.REPAIR,
                forceCurrent = true
            )
            if (startRequest != currentStartRequest() ||
                audioUiState.link.selected != LinkChoice.UNIVERSAL
            ) return
            linkManager.forgetWifiDirectPeer()
            P2pPairStore.save(activity, qr.token, qr.deviceName)
            audioUiState = audioUiState.copy(
                statusMessage = "已保存 ${qr.deviceName.ifBlank { "新电脑" }}，等待万能链路连接",
                link = audioUiState.link.copy(
                    hasP2pPairing = true,
                    active = null,
                    switching = true,
                    statusMessage = "等待万能链路连接"
                )
            )
            restart = shouldResume
        } finally {
            if (selectionPreparation === currentJob) selectionPreparation = null
        }
        if (restart) requestStart(startRequest)
    }

    fun stopStreaming() {
        selectionGeneration++
        resumeAfterSelection = false
        selectionPreparation?.cancel()
        selectionPreparation = null
        switchCoordinator.cancel()
        notifyActiveLinkStopped()
        mainScope.launch {
            operationMutex.withLock {
                linkManager.disconnect()
                stopProjectionPreparationService()
                audioUiState = audioUiState.copy(
                    streamButtonState = StreamButtonState.IDLE,
                    statusMessage = "已停止",
                    audioLevel = 0f,
                    longPressProgress = 0f,
                    link = audioUiState.link.copy(
                        active = null,
                        switching = false,
                        connectionState = ConnectionState.DISCONNECTED
                    ),
                    lanDevices = currentLanPanel(connectedDevice = null),
                )
            }
        }
    }

    fun setStreamVolume(value: Float): Float {
        val normalized = pipeline.setStreamVolume(value)
        streamGainStore.persist(normalized)
        audioUiState = audioUiState.copy(streamVolume = normalized)
        return normalized
    }

    fun adjustStreamVolume(delta: Float): Boolean = streamVolumeController.adjust(
        streaming = linkManager.isStreaming,
        current = pipeline.streamVolume(),
        delta = delta,
        onVolumeChanged = ::setStreamVolume,
        onHudVisibilityChanged = { visible ->
            audioUiState = audioUiState.copy(showVolumeHud = visible)
        },
    )

    fun adjustStreamVolumeUp(): Boolean = adjustStreamVolume(StreamGain.HARDWARE_KEY_STEP)
    fun adjustStreamVolumeDown(): Boolean = adjustStreamVolume(-StreamGain.HARDWARE_KEY_STEP)

    fun clearPairing(): String {
        P2pPairStore.clear(activity)
        linkManager.forgetWifiDirectPeer()
        audioUiState = audioUiState.copy(link = audioUiState.link.copy(hasP2pPairing = false))
        return "配对记录已清除"
    }

    fun resetConfiguration(): String {
        stopStreaming()
        P2pPairStore.clear(activity)
        linkManager.forgetWifiDirectPeer()
        activity.getSharedPreferences(UI_PREFS, Context.MODE_PRIVATE).edit().clear().apply()
        pipeline.setStreamVolume(1f)
        projectionOwner.clear(stopProjection = true)
        selectedLanDevice = currentLanPanel().discoveredDevices.firstOrNull()
        audioUiState = AudioUiState(
            streamVolume = 1f,
            targetDeviceName = selectedLanDevice?.displayName,
            lanDevices = currentLanPanel(connectedDevice = null),
        )
        return "配置已重置"
    }

    fun forceDisconnect(): String {
        stopStreaming()
        return "连接已断开"
    }

    fun networkDiagnostics(): String {
        val manager = activity.getSystemService(ConnectivityManager::class.java)
        val network = manager.activeNetwork ?: return "当前没有可用网络"
        val capabilities = manager.getNetworkCapabilities(network) ?: return "无法读取当前网络能力"
        val transport = when {
            capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) -> "Wi-Fi"
            capabilities.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) -> "蜂窝网络"
            capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) -> "以太网"
            else -> "其他网络"
        }
        val target = audioUiState.targetDeviceName ?: "未发现电脑"
        return "网络：$transport\n目标：$target\n发现设备：${devices.size}\n状态：${audioUiState.statusMessage}"
    }

    fun shareDiagnostics() {
        val intent = Intent(Intent.ACTION_SEND).apply {
            type = "text/plain"
            putExtra(Intent.EXTRA_SUBJECT, "墨堤诊断信息")
            putExtra(Intent.EXTRA_TEXT, diagnosticsText())
        }
        activity.startActivity(Intent.createChooser(intent, "导出诊断信息"))
    }

    fun diagnosticsText(): String = buildString {
        appendLine("墨堤 Android 诊断")
        appendLine(networkDiagnostics())
        appendLine("音频参数：${audioConfigLabel()}")
        appendLine("目标链路：${audioUiState.link.selected.title}")
        appendLine("活跃链路：${linkManager.activeLinkType?.let(::linkTypeLabel) ?: "无"}")
        appendLine()
        appendLine("最近应用日志：")
        append(ExportableLogger.snapshot())
    }

    fun audioConfigLabel(): String {
        val config = AudioConfig.DEFAULT
        return "${config.sampleRate / 1000} kHz · ${config.bitrate / 1000} kbps"
    }

    private fun currentConnectionIntent(): LinkConnectionIntent {
        val pair = P2pPairStore.load(activity)?.let { it.token to it.deviceName }
        return buildLinkConnectionIntent(
            choice = audioUiState.link.selected,
            route = audioUiState.selectedRoute,
            lanHost = selectedLanDevice?.host ?: currentLanPanel().discoveredDevices.firstOrNull()?.host,
            p2pPair = pair
        )
    }

    private fun onSwitchStatus(status: LinkSwitchStatus) {
        audioUiState = audioUiState.copy(
            streamButtonState = when {
                status.switching -> StreamButtonState.CONNECTING
                status.connected && linkManager.isStreaming -> StreamButtonState.STREAMING
                status.connected -> stateManager.state.toStreamButtonState()
                else -> StreamButtonState.ERROR
            },
            statusMessage = status.message,
            link = audioUiState.link.copy(
                selected = status.selected,
                active = when {
                    status.connected -> status.selected
                    status.switching -> audioUiState.link.active
                    else -> null
                },
                connectionState = stateManager.state,
                switching = status.switching,
                statusMessage = status.message
            ),
            lanDevices = currentLanPanel(
                connectedDevice = when {
                    status.selected == LinkChoice.HOME && status.connected -> selectedLanDevice
                    status.selected == LinkChoice.HOME && !status.switching -> null
                    else -> audioUiState.lanDevices.connectedDevice
                },
            ),
        )
    }

    private fun currentLanPanel(
        connectedDevice: LanDeviceUiModel? = audioUiState.lanDevices.connectedDevice,
    ): LanDevicePanelState = LanDevicePanelState.from(
        selectedEndpointId = selectedLanDevice?.endpointId,
        connectedDevice = connectedDevice,
        discoveredDevices = devices,
    )

    private fun linkTypeLabel(type: Byte): String = when (type) {
        LinkType.WIFI_LAN -> "Wi-Fi LAN"
        LinkType.WIFI_DIRECT -> "Wi-Fi Direct"
        LinkType.BLUETOOTH -> "蓝牙"
        LinkType.USB -> "USB"
        else -> "未知"
    }

    private fun stopProjectionPreparationService() {
        activity.stopService(Intent(activity, MediaProjectionService::class.java))
    }

    /**
     * 手动停止时向电脑端发送 USER_STOP 断连通知（best-effort）。
     * 不发的话 Windows 会保留旧会话直到看门狗超时，重连时触发
     * PauseEngine→ResumeEngine 循环；通知后电脑立即回到已断开状态。
     */
    private fun notifyActiveLinkStopped() {
        val activeType = linkManager.activeLinkType ?: return
        mainScope.launch {
            try {
                linkManager.notifyDisconnect(activeType, DisconnectReason.USER_STOP)
            } catch (_: Exception) {
                // 发送失败不阻塞本地停止
            }
        }
    }

    companion object {
        private const val UI_PREFS = "modi_ui"
    }
}
