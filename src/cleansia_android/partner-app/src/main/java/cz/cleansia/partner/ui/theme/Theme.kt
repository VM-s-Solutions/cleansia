package cz.cleansia.partner.ui.theme

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat
import cz.cleansia.core.ui.theme.CleansiaShapes
import cz.cleansia.core.ui.theme.CleansiaTypography

private val LightColors = lightColorScheme(
    primary = Sky600,
    onPrimary = LightSurface,
    primaryContainer = Sky100,
    onPrimaryContainer = Sky900,
    secondary = Sky400,
    onSecondary = LightSurface,
    secondaryContainer = Sky50,
    onSecondaryContainer = Sky900,
    background = LightBackground,
    onBackground = LightTextPrimary,
    surface = LightSurface,
    onSurface = LightTextPrimary,
    surfaceVariant = LightSurfaceVariant,
    onSurfaceVariant = LightTextBody,
    outline = LightBorder,
    outlineVariant = LightBorder,
    error = ErrorText,
    onError = LightSurface,
)

private val DarkColors = darkColorScheme(
    primary = Sky400,
    onPrimary = Sky900,
    primaryContainer = Sky700,
    onPrimaryContainer = Sky100,
    secondary = Sky300,
    onSecondary = Sky900,
    secondaryContainer = Sky800,
    onSecondaryContainer = Sky100,
    background = DarkBackground,
    onBackground = DarkTextPrimary,
    surface = DarkSurface,
    onSurface = DarkTextPrimary,
    surfaceVariant = DarkSurfaceElevated,
    onSurfaceVariant = DarkTextSecondary,
    outline = DarkBorder,
    outlineVariant = DarkBorder,
    error = Color(0xFFFCA5A5),
    onError = ErrorText,
)

@Composable
fun CleansiaPartnerTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit,
) {
    val colors = if (darkTheme) DarkColors else LightColors
    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as Activity).window
            window.statusBarColor = colors.background.toArgb()
            window.navigationBarColor = android.graphics.Color.TRANSPARENT
            val insetsController = WindowCompat.getInsetsController(window, view)
            insetsController.isAppearanceLightStatusBars = !darkTheme
            insetsController.isAppearanceLightNavigationBars = !darkTheme
        }
    }
    MaterialTheme(
        colorScheme = colors,
        typography = CleansiaTypography,
        shapes = CleansiaShapes,
        content = content,
    )
}
