package cz.cleansia.customer.ui.theme

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.ui.graphics.Color
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
    primary = Sky400, // brighter for WCAG AA on slate-900
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
fun CleansiaTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit,
) {
    val colors = if (darkTheme) DarkColors else LightColors
    val view = LocalView.current
    if (!view.isInEditMode) {
        // When the theme changes, not after every recomposition: a screen with a hero under the status
        // bar sets the icons itself (statusBarFade), and side effects run after every other effect in a
        // frame, so one here would undo it.
        DisposableEffect(darkTheme, view) {
            val window = (view.context as Activity).window
            // Both bars stay transparent so the system never paints over the
            // page: the root Surface already fills the whole window with
            // `background`, which is what a background-tinted status bar was
            // reproducing, and the profile hero's gradient runs under the
            // status bar the way iOS's does. targetSdk 35 ignores these two
            // setters outright — setting them keeps API 30-34 identical rather
            // than letting an opaque band clip the hero only on older devices.
            window.statusBarColor = android.graphics.Color.TRANSPARENT
            window.navigationBarColor = android.graphics.Color.TRANSPARENT
            val insetsController = WindowCompat.getInsetsController(window, view)
            insetsController.isAppearanceLightStatusBars = !darkTheme
            insetsController.isAppearanceLightNavigationBars = !darkTheme
            onDispose {}
        }
    }
    MaterialTheme(
        colorScheme = colors,
        typography = CleansiaTypography,
        shapes = CleansiaShapes,
        content = content,
    )
}

