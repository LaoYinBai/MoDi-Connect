package com.modi.connect.ui.audio

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.LocalIndication
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.waitForUpOrCancellation
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.WarningAmber
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.modi.connect.ui.model.StreamButtonState
import com.modi.connect.ui.model.acceptsStartTap
import kotlinx.coroutines.launch

@Composable
fun StreamButton(
    state: StreamButtonState,
    compact: Boolean,
    onStart: () -> Unit,
    onStop: () -> Unit,
    modifier: Modifier = Modifier
) {
    val size = if (compact) 64.dp else 72.dp
    val progress = remember { Animatable(0f) }
    val scope = rememberCoroutineScope()
    val interactionSource = remember { MutableInteractionSource() }
    val pressed by interactionSource.collectIsPressedAsState()
    val pressScale by animateFloatAsState(
        targetValue = if (pressed) .97f else 1f,
        animationSpec = tween(100),
        label = "StreamButtonPress"
    )
    val haptics = LocalHapticFeedback.current
    val colors = MaterialTheme.colorScheme
    val isStreaming = state == StreamButtonState.STREAMING

    LaunchedEffect(state) {
        if (!isStreaming) progress.snapTo(0f)
    }

    val interactionModifier = if (isStreaming) {
        Modifier.pointerInput(state) {
            awaitEachGesture {
                awaitFirstDown(requireUnconsumed = false)
                haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                val hold = scope.launch {
                    progress.snapTo(0f)
                    progress.animateTo(1f, tween(800, easing = LinearEasing))
                    haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                    onStop()
                }
                try {
                    waitForUpOrCancellation()
                } finally {
                    if (hold.isActive) {
                        hold.cancel()
                        scope.launch { progress.animateTo(0f, tween(300)) }
                    }
                }
            }
        }
    } else if (state.acceptsStartTap()) {
        Modifier.clickable(
            interactionSource = interactionSource,
            indication = LocalIndication.current,
            role = Role.Button,
            onClick = onStart
        )
    } else {
        Modifier
    }

    Box(
        contentAlignment = Alignment.Center,
        modifier = modifier
            .size(size + 12.dp)
            .semantics {
                contentDescription = when (state) {
                    StreamButtonState.IDLE -> "开始推流"
                    StreamButtonState.PERMISSION_REQUESTING -> "正在请求授权"
                    StreamButtonState.CONNECTING -> "正在连接"
                    StreamButtonState.STREAMING -> "长按停止推流"
                    StreamButtonState.ERROR -> "连接失败，点击重试"
                }
            }
            .then(interactionModifier)
    ) {
        if (isStreaming) {
            Canvas(Modifier.fillMaxSize()) {
                drawArc(
                    color = colors.onError,
                    startAngle = -90f,
                    sweepAngle = 360f * progress.value,
                    useCenter = false,
                    style = Stroke(width = 4.dp.toPx(), cap = StrokeCap.Round)
                )
            }
        }

        Surface(
            modifier = Modifier
                .size(size)
                .scale(if (isStreaming && progress.value > 0f) .97f else pressScale),
            shape = CircleShape,
            color = when (state) {
                StreamButtonState.STREAMING -> colors.error
                StreamButtonState.ERROR -> colors.errorContainer
                else -> colors.primary
            },
            contentColor = when (state) {
                StreamButtonState.STREAMING -> colors.onError
                StreamButtonState.ERROR -> colors.onErrorContainer
                else -> colors.onPrimary
            },
            shadowElevation = 8.dp
        ) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                when (state) {
                    StreamButtonState.PERMISSION_REQUESTING,
                    StreamButtonState.CONNECTING -> CircularProgressIndicator(
                        modifier = Modifier.size(size - 12.dp),
                        color = colors.tertiary,
                        strokeWidth = 3.dp
                    )
                    StreamButtonState.ERROR -> {
                        Icon(Icons.Outlined.WarningAmber, contentDescription = null, modifier = Modifier.align(Alignment.TopCenter))
                        Text("重试", style = MaterialTheme.typography.labelLarge, modifier = Modifier.align(Alignment.BottomCenter))
                    }
                    else -> Text(
                        text = if (state == StreamButtonState.STREAMING) "停止" else "开始\n推流",
                        style = MaterialTheme.typography.labelLarge
                    )
                }
            }
        }
    }
}
