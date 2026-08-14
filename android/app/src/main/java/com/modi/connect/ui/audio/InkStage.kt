package com.modi.connect.ui.audio

import android.animation.ValueAnimator
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.modi.connect.ui.model.StreamButtonState
import kotlinx.coroutines.delay
import kotlin.math.PI
import kotlin.math.sin

@Composable
fun InkStage(
    state: StreamButtonState,
    audioLevel: Float,
    modifier: Modifier = Modifier
) {
    val colors = MaterialTheme.colorScheme
    val lifecycleOwner = LocalLifecycleOwner.current
    var active by remember { mutableStateOf(lifecycleOwner.lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED)) }
    val connectionProgress = remember { Animatable(0f) }
    val animationsEnabled = ValueAnimator.areAnimatorsEnabled()
    var wavePhase by remember { mutableFloatStateOf(0f) }

    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            active = event == Lifecycle.Event.ON_RESUME ||
                (event != Lifecycle.Event.ON_PAUSE && lifecycleOwner.lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED))
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    LaunchedEffect(state, active, animationsEnabled) {
        val target = when (state) {
            StreamButtonState.CONNECTING -> .5f
            StreamButtonState.STREAMING -> 1f
            else -> 0f
        }
        if (!active) {
            connectionProgress.stop()
            return@LaunchedEffect
        }
        if (!animationsEnabled) {
            connectionProgress.snapTo(target)
        } else {
            connectionProgress.animateTo(
                target,
                tween(
                    durationMillis = if (target == .5f) 600 else 800,
                    easing = FastOutSlowInEasing
                )
            )
        }
    }

    LaunchedEffect(state, active, animationsEnabled) {
        while (active && animationsEnabled && state == StreamButtonState.STREAMING) {
            wavePhase = (wavePhase + 0.035f) % 1f
            delay(33)
        }
    }

    Canvas(
        modifier = modifier
            .fillMaxSize()
            .semantics {
                contentDescription = when (state) {
                    StreamButtonState.STREAMING -> "桥下水流随音频起伏，正在推流"
                    StreamButtonState.CONNECTING -> "山峰正在落笔，正在连接"
                    StreamButtonState.ERROR -> "桥仍在，连接已断开"
                    else -> "墨堤桥景，等待连接"
                }
            }
    ) {
        drawRect(Brush.linearGradient(listOf(colors.surface, colors.surfaceVariant)))
        repeat(4) { line ->
            val y = size.height * (.18f + line * .21f)
            drawLine(colors.onSurface.copy(alpha = .025f), Offset(0f, y), Offset(size.width, y - 14f), 1f)
        }

        val mountainProgress = (connectionProgress.value * 2f).coerceIn(0f, 1f)
        val waterProgress = ((connectionProgress.value - .5f) * 2f).coerceIn(0f, 1f)
        val mountainAlpha = .1f + .5f * mountainProgress
        val mountainOffset = -size.height * .2f * (1f - mountainProgress)
        drawMountain(size.width * .25f, size.height * .66f + mountainOffset, size.width * .34f, colors.onSurface.copy(alpha = mountainAlpha * .55f))
        drawMountain(size.width * .72f, size.height * .7f + mountainOffset, size.width * .39f, colors.onSurface.copy(alpha = mountainAlpha * .35f))

        val bridgeY = size.height * .68f
        val bridge = Path().apply {
            moveTo(size.width * .08f, bridgeY)
            cubicTo(size.width * .3f, bridgeY - size.height * .08f, size.width * .7f, bridgeY - size.height * .08f, size.width * .92f, bridgeY)
        }
        drawPath(bridge, colors.secondary.copy(alpha = .74f), style = Stroke(width = size.minDimension * .035f, cap = StrokeCap.Square))
        val arch = Path().apply {
            moveTo(size.width * .15f, bridgeY + size.height * .02f)
            cubicTo(size.width * .3f, size.height * .9f, size.width * .38f, size.height * .9f, size.width * .43f, bridgeY + size.height * .12f)
            cubicTo(size.width * .47f, bridgeY + size.height * .02f, size.width * .53f, bridgeY + size.height * .02f, size.width * .57f, bridgeY + size.height * .12f)
            cubicTo(size.width * .62f, size.height * .9f, size.width * .7f, size.height * .9f, size.width * .85f, bridgeY + size.height * .02f)
        }
        drawPath(arch, colors.secondary.copy(alpha = .68f), style = Stroke(width = size.minDimension * .012f, cap = StrokeCap.Round))
        repeat(9) { post ->
            val x = size.width * (.14f + post * .09f)
            drawLine(colors.secondary.copy(alpha = .48f), Offset(x, bridgeY - size.height * .055f), Offset(x, bridgeY + size.height * .015f), size.minDimension * .005f)
        }

        val waterTop = size.height * (1.04f - .26f * waterProgress)
        val energy = .25f + audioLevel.coerceIn(0f, 1f) * .75f
        repeat(3) { layer ->
            val wave = Path()
            val baseY = waterTop + layer * size.height * .045f
            val amplitude = size.height * (.012f + layer * .004f) * energy
            var x = -8f
            while (x <= size.width + 8f) {
                val y = baseY + sin((x / size.width * 2f * PI + wavePhase * 2f * PI + layer).toFloat()) * amplitude
                if (x < 0f) wave.moveTo(x, y) else wave.lineTo(x, y)
                x += 8f
            }
            drawPath(
                wave,
                colors.tertiary.copy(alpha = (.7f - layer * .17f) * waterProgress),
                style = Stroke(width = size.minDimension * (.014f - layer * .003f), cap = StrokeCap.Round)
            )
        }
    }
}

private fun androidx.compose.ui.graphics.drawscope.DrawScope.drawMountain(
    centerX: Float,
    baseY: Float,
    width: Float,
    color: androidx.compose.ui.graphics.Color
) {
    val path = Path().apply {
        moveTo(centerX - width, baseY)
        cubicTo(centerX - width * .45f, baseY - size.height * .23f, centerX - width * .2f, baseY - size.height * .3f, centerX, baseY - size.height * .27f)
        cubicTo(centerX + width * .3f, baseY - size.height * .23f, centerX + width * .55f, baseY - size.height * .06f, centerX + width, baseY)
        close()
    }
    drawPath(path, color)
}
