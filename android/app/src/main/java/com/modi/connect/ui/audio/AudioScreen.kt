package com.modi.connect.ui.audio

import android.content.res.Configuration
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.compose.ui.tooling.preview.Preview
import com.modi.connect.ui.model.AudioUiState
import com.modi.connect.ui.model.StreamButtonState
import com.modi.connect.ui.link.P2pScanButton
import com.modi.connect.ui.link.LinkSelectorButton
import com.modi.connect.ui.device.LanDeviceButton
import com.modi.connect.ui.model.LanDeviceUiModel
import com.modi.connect.ui.model.LinkChoice
import com.modi.connect.ui.model.LinkUiState
import com.modi.connect.ui.theme.MoDiTheme

@Composable
fun AudioScreen(
    uiState: AudioUiState,
    onSelectPipeline: (Int) -> Unit,
    onStart: () -> Unit,
    onStop: () -> Unit,
    onSelectLink: (LinkChoice) -> Unit = {},
    onSelectLanDevice: (LanDeviceUiModel) -> Unit = {},
    onScanP2p: () -> Unit = {},
    modifier: Modifier = Modifier
) {
    BoxWithConstraints(modifier.fillMaxSize()) {
        val compact = maxHeight < 640.dp
        Column(
            Modifier
                .fillMaxSize()
                .semantics {
                    liveRegion = LiveRegionMode.Polite
                    stateDescription = uiState.statusMessage
                }
        ) {
            InkStage(
                state = uiState.streamButtonState,
                audioLevel = uiState.audioLevel,
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(.55f)
            )

            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(.45f)
                    .padding(horizontal = 16.dp)
            ) {
                Column(
                    modifier = Modifier.weight(.70f),
                    verticalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    uiState.pipelines.chunked(2).forEach { rowItems ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            horizontalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            rowItems.forEach { item ->
                                PipelineCard(
                                    item = item,
                                    selected = uiState.selectedRoute == item.route,
                                    compact = compact,
                                    onClick = { onSelectPipeline(item.route) },
                                    modifier = Modifier.weight(1f)
                                )
                            }
                        }
                    }
                }
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .weight(.30f)
                        .padding(bottom = 32.dp),
                    contentAlignment = Alignment.BottomCenter
                ) {
                    StreamButton(
                        state = uiState.streamButtonState,
                        compact = compact,
                        onStart = onStart,
                        onStop = onStop
                    )
                }
            }
        }
        val rightTopModifier = Modifier
            .align(Alignment.TopEnd)
            .padding(top = 8.dp, end = 12.dp)
        when (uiState.link.selected) {
            LinkChoice.HOME -> LanDeviceButton(
                state = uiState.lanDevices,
                onSelectDevice = onSelectLanDevice,
                modifier = rightTopModifier,
            )
            LinkChoice.UNIVERSAL -> P2pScanButton(
                onClick = onScanP2p,
                modifier = rightTopModifier,
            )
            LinkChoice.BLUETOOTH, LinkChoice.USB -> Unit
        }
        LinkSelectorButton(
            state = uiState.link,
            onSelect = onSelectLink,
            modifier = Modifier
                .align(Alignment.TopStart)
                .padding(top = 8.dp, start = 12.dp)
        )
        StreamVolumeHud(
            visible = uiState.showVolumeHud,
            volume = uiState.streamVolume,
            modifier = Modifier
                .align(Alignment.Center)
                .padding(horizontal = 24.dp),
        )
    }
}

@Preview(name = "音频 · 待机", showBackground = true, widthDp = 393, heightDp = 852)
@Composable
private fun IdleAudioPreview() {
    AudioPreview(AudioUiState())
}

@Preview(name = "音频 · 推流", showBackground = true, widthDp = 393, heightDp = 852)
@Composable
private fun StreamingAudioPreview() {
    AudioPreview(
        AudioUiState(
            selectedRoute = 1,
            streamButtonState = StreamButtonState.STREAMING,
            audioLevel = .62f,
            statusMessage = "音频传输中"
        )
    )
}

@Preview(name = "音频 · 错误", showBackground = true, widthDp = 393, heightDp = 852)
@Composable
private fun ErrorAudioPreview() {
    AudioPreview(AudioUiState(streamButtonState = StreamButtonState.ERROR, statusMessage = "握手失败"))
}

@Preview(
    name = "音频 · 紧凑深色",
    showBackground = true,
    widthDp = 360,
    heightDp = 600,
    uiMode = Configuration.UI_MODE_NIGHT_YES
)
@Composable
private fun CompactDarkAudioPreview() {
    AudioPreview(
        AudioUiState(
            streamButtonState = StreamButtonState.CONNECTING,
            link = LinkUiState(selected = LinkChoice.UNIVERSAL, switching = true)
        ),
        darkTheme = true
    )
}

@Preview(name = "音频 · 万能等待扫码", showBackground = true, widthDp = 393, heightDp = 852)
@Composable
private fun UniversalAudioPreview() {
    AudioPreview(AudioUiState(link = LinkUiState(selected = LinkChoice.UNIVERSAL)))
}

@Preview(name = "音频 · 蓝牙连接中", showBackground = true, widthDp = 393, heightDp = 852)
@Composable
private fun BluetoothAudioPreview() {
    AudioPreview(
        AudioUiState(
            streamButtonState = StreamButtonState.CONNECTING,
            link = LinkUiState(selected = LinkChoice.BLUETOOTH, switching = true)
        )
    )
}

@Preview(name = "音频 · USB 推流", showBackground = true, widthDp = 393, heightDp = 852)
@Composable
private fun UsbAudioPreview() {
    AudioPreview(
        AudioUiState(
            streamButtonState = StreamButtonState.STREAMING,
            audioLevel = .5f,
            link = LinkUiState(selected = LinkChoice.USB, active = LinkChoice.USB)
        )
    )
}

@Composable
private fun AudioPreview(state: AudioUiState, darkTheme: Boolean = false) {
    MoDiTheme(darkTheme = darkTheme) {
        AudioScreen(
            uiState = state,
            onSelectPipeline = {},
            onStart = {},
            onStop = {}
        )
    }
}
