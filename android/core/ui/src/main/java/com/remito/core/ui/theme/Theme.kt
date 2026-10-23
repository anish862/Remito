package com.remito.core.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val LightColors =
    lightColorScheme(
        primary = BrandGreen,
        onPrimary = Color.White,
        background = SurfaceLight,
        surface = SurfaceLight,
    )

private val DarkColors =
    darkColorScheme(
        primary = BrandGreenDark,
        onPrimary = Color(0xFF00390B),
        background = SurfaceDark,
        surface = SurfaceDark,
    )

val RemitoTypography = Typography()

@Composable
fun RemitoTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = if (isSystemInDarkTheme()) DarkColors else LightColors,
        typography = RemitoTypography,
        content = content,
    )
}
