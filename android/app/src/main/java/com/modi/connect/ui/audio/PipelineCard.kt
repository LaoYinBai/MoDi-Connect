package com.modi.connect.ui.audio

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.LocalIndication
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.modi.connect.ui.model.PipelineOption

@Composable
fun PipelineCard(
    item: PipelineOption,
    selected: Boolean,
    compact: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val colors = MaterialTheme.colorScheme
    val shape = RoundedCornerShape(12.dp)
    val interactionSource = remember { MutableInteractionSource() }
    val pressed by interactionSource.collectIsPressedAsState()
    val pressScale by animateFloatAsState(
        targetValue = if (pressed) .97f else 1f,
        animationSpec = tween(100),
        label = "PipelinePress"
    )
    Surface(
        modifier = modifier
            .height(if (compact) 84.dp else 96.dp)
            .graphicsLayer {
                scaleX = pressScale
                scaleY = pressScale
            }
            .semantics { this.selected = selected }
            .clickable(
                interactionSource = interactionSource,
                indication = LocalIndication.current,
                role = Role.RadioButton,
                onClick = onClick
            ),
        shape = shape,
        color = if (selected) colors.primaryContainer.copy(alpha = if (colors.surface.luminance() > .5f) .08f else .12f) else Color.Transparent,
        border = BorderStroke(if (selected) 1.5.dp else 1.dp, if (selected) colors.primary else colors.outlineVariant)
    ) {
        Box(Modifier.fillMaxSize()) {
            if (selected) {
                Surface(
                    modifier = Modifier
                        .align(Alignment.CenterStart)
                        .width(4.dp)
                        .height(if (compact) 48.dp else 56.dp),
                    color = colors.primary,
                    shape = RoundedCornerShape(topEnd = 4.dp, bottomEnd = 4.dp)
                ) {}
            }
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .align(Alignment.CenterStart)
                    .padding(start = 17.dp, end = 12.dp)
            ) {
                Text(text = item.title, style = MaterialTheme.typography.titleMedium)
                Text(
                    text = item.direction,
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp)
                )
            }
        }
    }
}

private fun Color.luminance(): Float =
    (0.2126f * red + 0.7152f * green + 0.0722f * blue)
