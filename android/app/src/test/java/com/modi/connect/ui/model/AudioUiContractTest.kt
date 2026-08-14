package com.modi.connect.ui.model

import com.modi.connect.ConnectionState
import com.modi.connect.audio.AudioLevelMeter
import com.modi.connect.ui.theme.MoDiColors
import com.modi.connect.ui.theme.MoDiFontFamilies
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.text.font.FontFamily
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.nio.file.Files
import java.nio.file.Path

class AudioUiContractTest {

    @Test
    fun `brand colors remain identical across app themes`() {
        assertEquals(0xffe8863c.toInt(), MoDiColors.InkOrange.toArgb())
        assertEquals(0xff47937f.toInt(), MoDiColors.BridgeGreen.toArgb())
        assertEquals(0xff86bfd8.toInt(), MoDiColors.WaterBlue.toArgb())
        assertEquals(0xffc2452d.toInt(), MoDiColors.Cinnabar.toArgb())
    }

    @Test
    fun `cross platform typography uses bundled font roles instead of generic families`() {
        assertFalse(MoDiFontFamilies.title == FontFamily.Cursive)
        assertFalse(MoDiFontFamilies.body == FontFamily.Cursive)
        assertFalse(MoDiFontFamilies.annotation == FontFamily.Serif)
        assertFalse(MoDiFontFamilies.default == FontFamily.Serif)
        assertFalse(MoDiFontFamilies.function == FontFamily.SansSerif)
    }

    @Test
    fun `pipeline routes preserve the four real audio modes`() {
        val pipelines = PipelineOption.defaults()

        assertEquals(listOf(0, 1, 2, 3), pipelines.map { it.route })
        assertEquals(listOf("扬声器", "混音监听", "虚拟麦克风", "整活通道"), pipelines.map { it.title })
        assertEquals(
            listOf(
                "系统音频 → 电脑扬声器",
                "系统音频 + 麦克风 → 扬声器",
                "麦克风 → 电脑输入",
                "系统音频 → 电脑输入"
            ),
            pipelines.map { it.direction }
        )
    }

    @Test
    fun `pipeline permission requirements match actual capturers`() {
        val pipelines = PipelineOption.defaults().associateBy { it.route }

        assertTrue(pipelines.getValue(0).requiresMediaProjection)
        assertFalse(pipelines.getValue(0).requiresMicrophone)
        assertTrue(pipelines.getValue(1).requiresMediaProjection)
        assertTrue(pipelines.getValue(1).requiresMicrophone)
        assertFalse(pipelines.getValue(2).requiresMediaProjection)
        assertTrue(pipelines.getValue(2).requiresMicrophone)
        assertTrue(pipelines.getValue(3).requiresMediaProjection)
        assertFalse(pipelines.getValue(3).requiresMicrophone)
    }

    @Test
    fun `permission sequence requests only what the selected pipeline needs`() {
        val pipelines = PipelineOption.defaults().associateBy { it.route }

        assertEquals(PermissionRequirement.MEDIA_PROJECTION, pipelines.getValue(0).nextPermission(false, false))
        assertEquals(PermissionRequirement.MICROPHONE, pipelines.getValue(1).nextPermission(false, false))
        assertEquals(PermissionRequirement.MEDIA_PROJECTION, pipelines.getValue(1).nextPermission(true, false))
        assertEquals(PermissionRequirement.MICROPHONE, pipelines.getValue(2).nextPermission(false, false))
        assertEquals(PermissionRequirement.READY, pipelines.getValue(2).nextPermission(true, false))
        assertEquals(PermissionRequirement.READY, pipelines.getValue(3).nextPermission(false, true))
    }

    @Test
    fun `connection states map to visible stream button states`() {
        assertEquals(StreamButtonState.IDLE, ConnectionState.IDLE.toStreamButtonState())
        assertEquals(StreamButtonState.IDLE, ConnectionState.DISCONNECTED.toStreamButtonState())
        assertEquals(StreamButtonState.IDLE, ConnectionState.SEARCHING.toStreamButtonState())
        assertEquals(StreamButtonState.IDLE, ConnectionState.FOUND.toStreamButtonState())
        assertEquals(StreamButtonState.CONNECTING, ConnectionState.CONNECTING.toStreamButtonState())
        assertEquals(StreamButtonState.CONNECTING, ConnectionState.CONNECTED.toStreamButtonState())
        assertEquals(StreamButtonState.CONNECTING, ConnectionState.RECONNECTING.toStreamButtonState())
        assertEquals(StreamButtonState.STREAMING, ConnectionState.STREAMING.toStreamButtonState())
        assertEquals(StreamButtonState.ERROR, ConnectionState.ERROR.toStreamButtonState())
    }

    @Test
    fun `only idle and error stream buttons accept a start tap`() {
        assertTrue(StreamButtonState.IDLE.acceptsStartTap())
        assertTrue(StreamButtonState.ERROR.acceptsStartTap())
        assertFalse(StreamButtonState.PERMISSION_REQUESTING.acceptsStartTap())
        assertFalse(StreamButtonState.CONNECTING.acceptsStartTap())
        assertFalse(StreamButtonState.STREAMING.acceptsStartTap())
    }

    @Test
    fun `audio state carries link state without mixing it with route`() {
        val state = AudioUiState(
            selectedRoute = 3,
            link = LinkUiState(selected = LinkChoice.BLUETOOTH, active = LinkChoice.HOME)
        )

        assertEquals(3, state.selectedRoute)
        assertEquals(LinkChoice.BLUETOOTH, state.link.selected)
        assertEquals(LinkChoice.HOME, state.link.active)
    }

    @Test
    fun `silent PCM produces no visual energy`() {
        assertEquals(0f, AudioLevelMeter.fromPcm16Le(ByteArray(1920)), 0.0001f)
    }

    @Test
    fun `full scale PCM produces normalized visual energy`() {
        val pcm = ByteArray(1920)
        for (index in pcm.indices step 2) {
            pcm[index] = 0xff.toByte()
            pcm[index + 1] = 0x7f
        }

        assertEquals(1f, AudioLevelMeter.fromPcm16Le(pcm), 0.001f)
    }

    @Test
    fun `odd trailing PCM byte is ignored safely`() {
        assertEquals(0f, AudioLevelMeter.fromPcm16Le(byteArrayOf(0, 0, 127)), 0.0001f)
    }

    @Test
    fun `audio screen stays behind callback boundary from real links`() {
        val source = String(Files.readAllBytes(
            Path.of("src/main/java/com/modi/connect/ui/audio/AudioScreen.kt")
        ))

        assertTrue(source.contains("onSelectLink: (LinkChoice) -> Unit"))
        assertFalse(source.contains("import com.modi.connect.links.LinkManager"))
        assertFalse(source.contains("import com.modi.connect.net.P2pPairStore"))
        assertFalse(source.contains("Transport"))
    }

    @Test
    fun `audio screen exposes mutually exclusive LAN device and universal scan actions`() {
        val source = String(Files.readAllBytes(
            Path.of("src/main/java/com/modi/connect/ui/audio/AudioScreen.kt")
        ))

        assertTrue(source.contains("LinkChoice.HOME -> LanDeviceButton"))
        assertTrue(source.contains("LinkChoice.UNIVERSAL -> P2pScanButton"))
        assertTrue(source.contains("onSelectLanDevice: (LanDeviceUiModel) -> Unit"))
        assertFalse(source.contains("import com.modi.connect.net.MoDiDiscovery"))
    }
}
