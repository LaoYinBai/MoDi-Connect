package com.modi.connect.ui.audio

import android.animation.ValueAnimator
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import kotlin.math.roundToInt

@Composable
fun StreamVolumeHud(visible: Boolean, volume: Float, modifier: Modifier = Modifier) {
    val percentage = (volume.coerceIn(0f, 1f) * 100).roundToInt()
    val content: @Composable () -> Unit = {
        Surface(
            modifier = modifier.semantics { contentDescription = "推流音量 $percentage%" },
            shape = RoundedCornerShape(18.dp),
            color = MaterialTheme.colorScheme.inverseSurface.copy(alpha = 0.92f),
            contentColor = MaterialTheme.colorScheme.inverseOnSurface,
            tonalElevation = 6.dp,
        ) {
            Text(
                text = "推流音量  $percentage%",
                style = MaterialTheme.typography.titleMedium,
                modifier = Modifier.padding(horizontal = 20.dp, vertical = 12.dp),
            )
        }
    }
    if (ValueAnimator.areAnimatorsEnabled()) {
        AnimatedVisibility(visible = visible, enter = fadeIn(), exit = fadeOut()) { content() }
    } else if (visible) {
        content()
    }
}
