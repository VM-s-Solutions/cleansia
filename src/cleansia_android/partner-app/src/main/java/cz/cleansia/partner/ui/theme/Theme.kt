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

internal val LightColors = lightColorScheme(
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
    // Inverted pair — the transient-overlay surface (snackbar pill). M3's baseline
    // is a purple-tinted grey that clashes with the Sky/Slate ramp, so pin it to
    // ours: a near-black pill on a light page.
    inverseSurface = Slate900,
    inverseOnSurface = Slate50,
    // The surface-container ramp, for the same reason: unset, a dialog (High) is M3's
    // #ECE6F0 and a menu (Container) #F3EDF7. Ours is the slate family, the dialog and
    // menu on slate-100 — the light grey of an iOS alert — and the highest step (a
    // switch's off track) on slate-200.
    surfaceContainerLowest = LightSurface,
    surfaceContainerLow = Slate50,
    surfaceContainer = Slate100,
    surfaceContainerHigh = Slate100,
    surfaceContainerHighest = Slate200,
    surfaceBright = LightSurface,
    surfaceDim = Slate200,
)

internal val DarkColors = darkColorScheme(
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
    // Mirror of the light scheme: a near-white pill on the slate-900 page.
    inverseSurface = Slate50,
    inverseOnSurface = Slate900,
    // A dialog and a menu sit one step above the slate-800 card, on the elevated slate the
    // dark scheme already uses for surfaceVariant, as an iOS alert sits above its page.
    surfaceContainerLowest = DarkBackground,
    surfaceContainerLow = DarkSurface,
    surfaceContainer = DarkSurfaceElevated,
    surfaceContainerHigh = DarkSurfaceElevated,
    surfaceContainerHighest = Slate700,
    surfaceBright = Slate700,
    surfaceDim = DarkBackground,
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
