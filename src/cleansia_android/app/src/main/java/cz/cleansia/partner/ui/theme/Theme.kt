package cz.cleansia.partner.ui.theme

import android.app.Activity
import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat

/**
 * CompositionLocal that provides the current dark theme state.
 * Use this instead of isSystemInDarkTheme() to respect in-app theme settings.
 */
val LocalDarkTheme = compositionLocalOf { false }

private val LightColorScheme = lightColorScheme(
    primary = Primary,
    onPrimary = OnPrimary,
    primaryContainer = PrimaryContainer,
    onPrimaryContainer = OnPrimaryContainer,

    secondary = Secondary,
    onSecondary = OnSecondary,
    secondaryContainer = SecondaryContainer,
    onSecondaryContainer = OnSecondaryContainer,

    tertiary = Info,
    onTertiary = OnInfo,
    tertiaryContainer = InfoContainer,
    onTertiaryContainer = OnInfoContainer,

    error = Error,
    onError = OnError,
    errorContainer = ErrorContainer,
    onErrorContainer = OnErrorContainer,

    background = Background,
    onBackground = OnBackground,
    surface = Surface,
    onSurface = OnSurface,
    surfaceVariant = SurfaceVariant,
    onSurfaceVariant = OnSurfaceVariant,

    outline = Outline,
    outlineVariant = OutlineVariant,
    scrim = Scrim
)

private val DarkColorScheme = darkColorScheme(
    primary = PrimaryDark_Theme,
    onPrimary = OnPrimaryDark_Theme,
    primaryContainer = PrimaryContainerDark,
    onPrimaryContainer = OnPrimaryContainerDark,

    secondary = SecondaryDark_Theme,
    onSecondary = OnSecondaryDark_Theme,
    secondaryContainer = SecondaryContainerDark,
    onSecondaryContainer = OnSecondaryContainerDark,

    tertiary = InfoLight,
    onTertiary = OnInfoContainer,
    tertiaryContainer = Info,
    onTertiaryContainer = InfoLight,

    error = ErrorDark,
    onError = OnError,
    errorContainer = ErrorContainerDark,
    onErrorContainer = OnErrorContainerDark,

    background = BackgroundDark,
    onBackground = OnBackgroundDark,
    surface = SurfaceDark,
    onSurface = OnSurfaceDark,
    surfaceVariant = SurfaceVariantDark,
    onSurfaceVariant = OnSurfaceVariantDark,

    outline = OutlineDark,
    outlineVariant = OutlineVariantDark,
    scrim = Scrim
)

@Composable
fun CleansiaTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    dynamicColor: Boolean = false,
    content: @Composable () -> Unit
) {
    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }
        darkTheme -> DarkColorScheme
        else -> LightColorScheme
    }

    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as Activity).window
            WindowCompat.getInsetsController(window, view).isAppearanceLightStatusBars = !darkTheme
        }
    }

    CompositionLocalProvider(LocalDarkTheme provides darkTheme) {
        MaterialTheme(
            colorScheme = colorScheme,
            typography = Typography,
            content = content
        )
    }
}

/**
 * Extended color properties for status indicators.
 * Theme-aware: composable getters return dark/light variants automatically.
 */
object CleansiaColors {
    val Primary = cz.cleansia.partner.ui.theme.Primary
    val PrimaryLight = cz.cleansia.partner.ui.theme.PrimaryLight
    val PrimaryDark = cz.cleansia.partner.ui.theme.PrimaryDark

    val Secondary = cz.cleansia.partner.ui.theme.Secondary
    val SecondaryLight = cz.cleansia.partner.ui.theme.SecondaryLight
    val SecondaryDark = cz.cleansia.partner.ui.theme.SecondaryDark

    // Static light-mode values (kept for backward compat)
    val Success = cz.cleansia.partner.ui.theme.Success
    val SuccessContainer = cz.cleansia.partner.ui.theme.SuccessContainer
    val OnSuccess = cz.cleansia.partner.ui.theme.OnSuccess
    val OnSuccessContainer = cz.cleansia.partner.ui.theme.OnSuccessContainer

    val Warning = cz.cleansia.partner.ui.theme.Warning
    val WarningContainer = cz.cleansia.partner.ui.theme.WarningContainer
    val OnWarning = cz.cleansia.partner.ui.theme.OnWarning
    val OnWarningContainer = cz.cleansia.partner.ui.theme.OnWarningContainer

    val Info = cz.cleansia.partner.ui.theme.Info
    val InfoContainer = cz.cleansia.partner.ui.theme.InfoContainer
    val OnInfo = cz.cleansia.partner.ui.theme.OnInfo
    val OnInfoContainer = cz.cleansia.partner.ui.theme.OnInfoContainer

    // Theme-aware composable getters (use LocalDarkTheme to respect in-app theme settings)
    val success: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) SuccessDark else cz.cleansia.partner.ui.theme.Success
    val successContainer: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) SuccessContainerDark else cz.cleansia.partner.ui.theme.SuccessContainer
    val onSuccessContainer: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) OnSuccessContainerDark else cz.cleansia.partner.ui.theme.OnSuccessContainer

    val warning: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) WarningDark else cz.cleansia.partner.ui.theme.Warning
    val warningContainer: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) WarningContainerDark else cz.cleansia.partner.ui.theme.WarningContainer
    val onWarningContainer: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) OnWarningContainerDark else cz.cleansia.partner.ui.theme.OnWarningContainer

    val info: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) InfoDark else cz.cleansia.partner.ui.theme.Info
    val infoContainer: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) InfoContainerDark else cz.cleansia.partner.ui.theme.InfoContainer
    val onInfoContainer: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) OnInfoContainerDark else cz.cleansia.partner.ui.theme.OnInfoContainer

    val purple: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) PurpleDark else Purple
    val cyan: androidx.compose.ui.graphics.Color
        @Composable get() = if (LocalDarkTheme.current) CyanDark else Cyan
}
