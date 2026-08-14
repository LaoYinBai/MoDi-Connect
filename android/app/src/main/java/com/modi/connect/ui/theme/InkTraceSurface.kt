package com.modi.connect.ui.theme

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.unit.dp

@Composable
fun InkTraceSurface(
    modifier: Modifier = Modifier,
    content: @Composable () -> Unit
) {
    val colors = MaterialTheme.colorScheme
    Box(modifier.fillMaxSize()) {
        Canvas(Modifier.fillMaxSize()) {
            drawRect(colors.background)
            val step = 22.dp.toPx()
            var row = 0
            var y = step * .5f
            while (y < size.height) {
                var column = 0
                var x = step * .5f
                while (x < size.width) {
                    val jitterX = (((row * 17 + column * 11) % 9) - 4) * density
                    val jitterY = (((row * 7 + column * 19) % 7) - 3) * density
                    drawCircle(
                        color = colors.onBackground.copy(alpha = .018f),
                        radius = if ((row + column) % 3 == 0) 1.2f * density else .7f * density,
                        center = Offset(x + jitterX, y + jitterY)
                    )
                    x += step
                    column++
                }
                y += step
                row++
            }
            repeat(5) { index ->
                val lineY = size.height * (.12f + index * .19f)
                drawLine(
                    color = colors.onBackground.copy(alpha = .012f),
                    start = Offset(-20f, lineY),
                    end = Offset(size.width + 20f, lineY - 10.dp.toPx()),
                    strokeWidth = .7.dp.toPx()
                )
            }
        }
        content()
    }
}
