package com.modi.connect.ui.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext

private val DarkColorScheme = darkColorScheme(
    primary = MoDiColors.InkOrange,
    onPrimary = Color.White,
    secondary = MoDiColors.BridgeGreen,
    onSecondary = Color.White,
    tertiary = MoDiColors.WaterBlue,
    onTertiary = MoDiColors.InkText,
    error = MoDiColors.Cinnabar,
    surface = MoDiColors.InkNight,
    surfaceContainer = MoDiColors.InkCard,
    surfaceContainerHigh = MoDiColors.InkCardSecondary,
    surfaceVariant = MoDiColors.InkCardSecondary,
    onSurface = MoDiColors.PaperText,
    onSurfaceVariant = MoDiColors.NightSecondaryText,
    outlineVariant = MoDiColors.InkBorder,
    background = MoDiColors.InkNight,
    onBackground = MoDiColors.PaperText
)

private val LightColorScheme = lightColorScheme(
    primary = MoDiColors.InkOrange,
    onPrimary = Color.White,
    secondary = MoDiColors.BridgeGreen,
    onSecondary = Color.White,
    tertiary = MoDiColors.WaterBlue,
    onTertiary = MoDiColors.InkText,
    error = MoDiColors.Cinnabar,
    surface = MoDiColors.PaperDay,
    surfaceContainer = MoDiColors.PaperCard,
    surfaceContainerHigh = MoDiColors.PaperCardSecondary,
    surfaceVariant = MoDiColors.PaperCardSecondary,
    onSurface = MoDiColors.InkText,
    onSurfaceVariant = MoDiColors.DaySecondaryText,
    outlineVariant = MoDiColors.PaperBorder,
    background = MoDiColors.PaperDay,
    onBackground = MoDiColors.InkText
)

@Composable
fun MoDiTheme(
    darkTheme: Boolean = LocalThemeSelection.current.mode.isDark(isSystemInDarkTheme()),
    dynamicColor: Boolean = false,
    content: @Composable () -> Unit
) {
    val base = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }
        darkTheme -> DarkColorScheme
        else -> LightColorScheme
    }
    val colorScheme = base.copy(
        primary = MoDiColors.InkOrange,
        secondary = MoDiColors.BridgeGreen,
        tertiary = MoDiColors.WaterBlue,
        error = MoDiColors.Cinnabar
    )

    MaterialTheme(
        colorScheme = colorScheme,
        typography = MoDiTypography,
        content = content
    )
}
