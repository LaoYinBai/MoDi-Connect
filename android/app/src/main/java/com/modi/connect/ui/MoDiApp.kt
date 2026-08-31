package com.modi.connect.ui

import android.Manifest
import android.animation.ValueAnimator
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.media.projection.MediaProjectionManager
import android.net.Uri
import android.os.Build
import com.modi.connect.BuildConfig
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.togetherWith
import androidx.compose.animation.core.tween
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.layout.padding
import androidx.core.content.ContextCompat
import com.modi.connect.MediaProjectionService
import com.modi.connect.ui.audio.AudioScreen
import com.modi.connect.ui.model.PermissionRequirement
import com.modi.connect.ui.model.LinkRuntimePermission
import com.modi.connect.ui.model.StreamButtonState
import com.modi.connect.ui.model.nextPermission
import com.modi.connect.ui.model.runtimePermission
import com.modi.connect.ui.navigation.AppBottomNavigation
import com.modi.connect.ui.navigation.AppDestination
import com.modi.connect.ui.onboarding.OnboardingScreen
import com.modi.connect.ui.onboarding.OnboardingStore
import com.modi.connect.ui.onboarding.SharedPreferencesOnboardingPersistence
import com.modi.connect.ui.profile.ProfileScreen
import com.modi.connect.ui.profile.ProfileLibrary
import com.modi.connect.ui.profile.ProfileReaderScreen
import com.modi.connect.ui.runtime.MoDiRuntime
import com.modi.connect.ui.runtime.LinkStartRequest
import com.modi.connect.ui.link.P2pScannerScreen
import com.modi.connect.ui.model.LinkChoice
import com.modi.connect.ui.settings.InformationDialog
import com.modi.connect.ui.settings.SettingsScreen
import com.modi.connect.ui.theme.InkTraceSurface
import kotlinx.coroutines.launch

private sealed interface PendingAudioAction {
    val route: Int

    data class Start(
        override val route: Int,
        val request: LinkStartRequest
    ) : PendingAudioAction
    data class Switch(override val route: Int) : PendingAudioAction
}

@Composable
fun MoDiApp(onRuntimeReady: (MoDiRuntime?) -> Unit = {}) {
    val activity = LocalContext.current as ComponentActivity
    @Suppress("DEPRECATION")
    val packageInfo = remember(activity) { activity.packageManager.getPackageInfo(activity.packageName, 0) }
    val versionName = packageInfo.versionName ?: "未知版本"
    val buildIdentity = "Build ${packageInfo.longVersionCode} · ${BuildConfig.MODI_COMMIT_SHA}"
    val runtime = remember(activity) { MoDiRuntime(activity) }
    val onboardingStore = remember(activity) {
        OnboardingStore(SharedPreferencesOnboardingPersistence(activity))
    }
    DisposableEffect(runtime) {
        onRuntimeReady(runtime)
        onDispose { onRuntimeReady(null) }
    }
    val projectionManager = remember(activity) {
        activity.getSystemService(MediaProjectionManager::class.java)
    }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val pageRisePixels = with(LocalDensity.current) { 12.dp.roundToPx() }
    val animationsEnabled = ValueAnimator.areAnimatorsEnabled()

    var destination by rememberSaveable { mutableStateOf(AppDestination.AUDIO) }
    var developerModeEnabled by rememberSaveable { mutableStateOf(false) }
    var profileLibrary by rememberSaveable { mutableStateOf<ProfileLibrary?>(null) }
    var pendingAudioAction by remember { mutableStateOf<PendingAudioAction?>(null) }
    var inFlightAudioAction by remember { mutableStateOf<PendingAudioAction?>(null) }
    var permissionRequestInFlight by remember { mutableStateOf(false) }
    var permissionRevision by remember { mutableIntStateOf(0) }
    var showP2pScanner by remember { mutableStateOf(false) }
    var pendingP2pScanPermission by remember { mutableStateOf(false) }
    var showOnboarding by remember { mutableStateOf(onboardingStore.shouldShow()) }

    fun showMessage(message: String) {
        scope.launch { snackbarHostState.showSnackbar(message) }
    }

    DisposableEffect(runtime) {
        runtime.start()
        onDispose { runtime.close() }
    }

    val microphoneLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        val requestedAction = inFlightAudioAction
        inFlightAudioAction = null
        permissionRequestInFlight = false
        if (!granted && requestedAction != null && pendingAudioAction == requestedAction) {
            runtime.cancelPermissionRequest("麦克风权限被拒绝，已保持新的目标链路")
            pendingAudioAction = null
        }
        permissionRevision++
    }

    val linkPermissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        val requestedAction = inFlightAudioAction
        inFlightAudioAction = null
        permissionRequestInFlight = false
        if (!granted && requestedAction != null && pendingAudioAction == requestedAction) {
            runtime.cancelPermissionRequest("链路权限被拒绝，已保持新的目标链路")
            pendingAudioAction = null
        }
        permissionRevision++
    }

    val cameraPermissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        pendingP2pScanPermission = false
        if (granted && runtime.audioUiState.link.selected == LinkChoice.UNIVERSAL) {
            showP2pScanner = true
        } else if (!granted) {
            runtime.cancelPermissionRequest("相机权限被拒绝，仍保持万能模式")
        }
    }

    val projectionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        val requestedAction = inFlightAudioAction
        inFlightAudioAction = null
        permissionRequestInFlight = false
        if (result.resultCode == Activity.RESULT_OK && result.data != null) {
            runCatching {
                projectionManager.getMediaProjection(result.resultCode, result.data!!)
            }.onSuccess { projection ->
                if (projection != null) runtime.setMediaProjection(projection)
                else if (requestedAction != null && pendingAudioAction == requestedAction) {
                    activity.stopService(Intent(activity, MediaProjectionService::class.java))
                    runtime.cancelPermissionRequest("系统音频授权失败，已保持新的目标链路")
                    pendingAudioAction = null
                }
            }.onFailure {
                if (requestedAction != null && pendingAudioAction == requestedAction) {
                    activity.stopService(Intent(activity, MediaProjectionService::class.java))
                    runtime.cancelPermissionRequest("系统音频授权失败：${it.message ?: "未知错误"}")
                    pendingAudioAction = null
                }
            }
        } else if (requestedAction != null && pendingAudioAction == requestedAction) {
            activity.stopService(Intent(activity, MediaProjectionService::class.java))
            runtime.cancelPermissionRequest("已取消系统音频授权，已保持新的目标链路")
            pendingAudioAction = null
        }
        permissionRevision++
    }

    LaunchedEffect(pendingAudioAction, permissionRevision) {
        val action = pendingAudioAction ?: return@LaunchedEffect
        if (permissionRequestInFlight) return@LaunchedEffect
        val option = runtime.audioUiState.pipelines.firstOrNull { it.route == action.route }
            ?: return@LaunchedEffect
        val hasMicrophonePermission = ContextCompat.checkSelfPermission(
            activity,
            Manifest.permission.RECORD_AUDIO
        ) == PackageManager.PERMISSION_GRANTED

        val linkPermission = when (
            runtime.audioUiState.link.selected.runtimePermission(Build.VERSION.SDK_INT)
        ) {
            LinkRuntimePermission.NEARBY_WIFI_DEVICES -> Manifest.permission.NEARBY_WIFI_DEVICES
            LinkRuntimePermission.FINE_LOCATION -> Manifest.permission.ACCESS_FINE_LOCATION
            LinkRuntimePermission.BLUETOOTH_CONNECT -> Manifest.permission.BLUETOOTH_CONNECT
            null -> null
        }

        if (linkPermission != null && ContextCompat.checkSelfPermission(
                activity,
                linkPermission
            ) != PackageManager.PERMISSION_GRANTED
        ) {
            permissionRequestInFlight = true
            inFlightAudioAction = action
            runtime.setPermissionRequesting(true)
            linkPermissionLauncher.launch(linkPermission)
            return@LaunchedEffect
        }

        when (option.nextPermission(hasMicrophonePermission, runtime.hasMediaProjection)) {
            PermissionRequirement.MICROPHONE -> {
                permissionRequestInFlight = true
                inFlightAudioAction = action
                runtime.setPermissionRequesting(true)
                microphoneLauncher.launch(Manifest.permission.RECORD_AUDIO)
            }

            PermissionRequirement.MEDIA_PROJECTION -> {
                permissionRequestInFlight = true
                inFlightAudioAction = action
                runtime.setPermissionRequesting(true)
                runCatching {
                    ContextCompat.startForegroundService(
                        activity,
                        Intent(activity, MediaProjectionService::class.java)
                    )
                    projectionLauncher.launch(projectionManager.createScreenCaptureIntent())
                }.onFailure {
                    permissionRequestInFlight = false
                    inFlightAudioAction = null
                    if (pendingAudioAction == action) {
                        pendingAudioAction = null
                        runtime.cancelPermissionRequest("无法请求系统音频授权：${it.message ?: "未知错误"}")
                    }
                }
            }

            PermissionRequirement.READY -> {
                pendingAudioAction = null
                runtime.setPermissionRequesting(false)
                runtime.selectPipeline(action.route)
                when (action) {
                    is PendingAudioAction.Start -> runtime.requestStart(action.request)
                    is PendingAudioAction.Switch -> Unit
                }
            }
        }
    }

    BackHandler(enabled = destination == AppDestination.SETTINGS) {
        destination = AppDestination.PROFILE
    }

    InkTraceSurface {
        if (showOnboarding) {
            val hasMicrophone = ContextCompat.checkSelfPermission(
                activity,
                Manifest.permission.RECORD_AUDIO,
            ) == PackageManager.PERMISSION_GRANTED
            val onboardingOption = runtime.audioUiState.pipelines.first()
            OnboardingScreen(
                permissionRequirement = onboardingOption.nextPermission(hasMicrophone, runtime.hasMediaProjection),
                hasMicrophonePermission = hasMicrophone,
                hasMediaProjection = runtime.hasMediaProjection,
                batteryOptimizationIgnored = runtime.batteryOptimizationIgnored,
                muteRecoveryPending = runtime.muteRecoveryPending,
                onRequestMicrophone = { microphoneLauncher.launch(Manifest.permission.RECORD_AUDIO) },
                onRequestMediaProjection = {
                    runCatching {
                        ContextCompat.startForegroundService(activity, Intent(activity, MediaProjectionService::class.java))
                        projectionLauncher.launch(projectionManager.createScreenCaptureIntent())
                    }.onFailure { showMessage("无法请求系统音频授权") }
                },
                onOpenKeepAliveSettings = { showMessage(runtime.openKeepAliveSettings()) },
                onComplete = {
                    onboardingStore.complete()
                    showOnboarding = false
                },
                onSkip = {
                    onboardingStore.skip()
                    showOnboarding = false
                },
            )
        } else if (profileLibrary != null) {
            ProfileReaderScreen(profileLibrary!!) { profileLibrary = null }
        } else Scaffold(
            containerColor = Color.Transparent,
            snackbarHost = { SnackbarHost(snackbarHostState) },
            bottomBar = {
                if (destination != AppDestination.SETTINGS) {
                    AppBottomNavigation(destination) { destination = it }
                }
            }
        ) { contentPadding ->
            val screenModifier = Modifier.padding(contentPadding)
            AnimatedContent(
                targetState = destination,
                transitionSpec = {
                    if (animationsEnabled) {
                        (fadeIn(tween(240)) + slideInVertically(tween(240)) { pageRisePixels })
                            .togetherWith(fadeOut(tween(120)))
                    } else {
                        fadeIn(tween(0)).togetherWith(fadeOut(tween(0)))
                    }
                },
                label = "MoDiPageTransition"
            ) { activeDestination ->
                when (activeDestination) {
                    AppDestination.AUDIO -> AudioScreen(
                        uiState = runtime.audioUiState,
                        onSelectPipeline = { route ->
                            when (runtime.audioUiState.streamButtonState) {
                                StreamButtonState.CONNECTING,
                                StreamButtonState.PERMISSION_REQUESTING -> showMessage("当前操作完成后才能切换通道")
                                StreamButtonState.STREAMING -> pendingAudioAction = PendingAudioAction.Switch(route)
                                else -> runtime.selectPipeline(route)
                            }
                        },
                        onStart = {
                            pendingAudioAction = PendingAudioAction.Start(
                                runtime.audioUiState.selectedRoute,
                                runtime.currentStartRequest()
                            )
                        },
                        onStop = runtime::stopStreaming,
                        onSelectLink = { choice ->
                            scope.launch {
                                runtime.selectLink(choice)?.let { request ->
                                    pendingAudioAction = PendingAudioAction.Start(
                                        runtime.audioUiState.selectedRoute,
                                        request
                                    )
                                }
                            }
                        },
                        onSelectLanDevice = { device ->
                            scope.launch {
                                runtime.selectLanDevice(device)?.let { request ->
                                    pendingAudioAction = PendingAudioAction.Start(
                                        runtime.audioUiState.selectedRoute,
                                        request,
                                    )
                                }
                            }
                        },
                        onScanP2p = {
                            if (runtime.audioUiState.link.selected != LinkChoice.UNIVERSAL || pendingP2pScanPermission) {
                                return@AudioScreen
                            }
                            if (ContextCompat.checkSelfPermission(activity, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) {
                                showP2pScanner = true
                            } else {
                                pendingP2pScanPermission = true
                                cameraPermissionLauncher.launch(Manifest.permission.CAMERA)
                            }
                        },
                        modifier = screenModifier
                    )

                    AppDestination.PROFILE -> ProfileScreen(
                        onStory = {
                            profileLibrary = ProfileLibrary.STORIES
                        },
                        onSponsors = {
                            profileLibrary = ProfileLibrary.SPONSORS
                        },
                        onSupport = {
                            profileLibrary = ProfileLibrary.SUPPORT
                        },
                        onWebsite = {
                            val intent = Intent(
                                Intent.ACTION_VIEW,
                                Uri.parse("https://modiconnect.cn")
                            )
                            runCatching { activity.startActivity(intent) }
                                .onFailure { showMessage("无法打开官网") }
                        },
                        onSettings = { destination = AppDestination.SETTINGS },
                        modifier = screenModifier
                    )

                    AppDestination.SETTINGS -> SettingsScreen(
                        versionName = versionName,
                        buildIdentity = buildIdentity,
                        audioConfig = runtime.audioConfigLabel(),
                        streaming = runtime.audioUiState.streamButtonState == StreamButtonState.STREAMING,
                        developerModeEnabled = developerModeEnabled,
                        onDeveloperModeEnabled = { developerModeEnabled = true },
                        onBack = { destination = AppDestination.PROFILE },
                        onExportLogs = runtime::shareDiagnostics,
                        onNetworkDiagnostics = runtime::networkDiagnostics,
                        onOpenKeepAliveSettings = runtime::openKeepAliveSettings,
                        onClearPairing = runtime::clearPairing,
                        onResetConfiguration = runtime::resetConfiguration,
                        onResetOnboarding = {
                            onboardingStore.reset()
                            showOnboarding = true
                            "新手引导已重置"
                        },
                        onForceDisconnect = runtime::forceDisconnect,
                        onMessage = ::showMessage,
                        modifier = screenModifier
                    )
                }
            }
        }
    }

    if (runtime.audioUiState.showKeepAliveGuide) {
        InformationDialog(
            title = "后台推流被中断",
            message = "检测到上次推流可能被系统清理。请到“设置 → 调试 → 后台运行设置”允许自启动、后台活动并关闭针对墨堤互联的电量限制。",
            onDismiss = runtime::dismissKeepAliveGuide,
        )
    }

    if (showP2pScanner && runtime.audioUiState.link.selected == LinkChoice.UNIVERSAL) {
        P2pScannerScreen(
            onScanned = { qr ->
                showP2pScanner = false
                scope.launch { runtime.applyP2pPair(qr) }
            },
            onDismiss = { showP2pScanner = false }
        )
    }
}
