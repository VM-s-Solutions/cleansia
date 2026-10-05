package cz.cleansia.core.ui.theme

import androidx.compose.material3.ColorScheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance

/**
 * Brand-identity colours that are the same in both apps and in both schemes, so
 * they cannot come from `MaterialTheme.colorScheme`: the launch gradient always
 * reads white-on-sky regardless of the device theme, exactly as the iOS
 * `CleansiaColors.splashGradientStart/End` pair does.
 */
val SplashGradientStart = Color(0xFF0284C7) // sky-600
val SplashGradientEnd = Color(0xFF38BDF8) // sky-400

/** sky-700: the light scheme's brand blue for TEXT (see [primaryText]). */
val PrimaryTextLight = Color(0xFF0369A1)

/**
 * The brand blue for TEXT in this scheme — blue copy, a text link, the label of a text or outlined
 * button and the icon beside it. The light primary, sky-600, reads 4.10:1 on white, under WCAG AA's
 * 4.5:1, so a light scheme gives sky-700 (5.93:1 on white); a dark one gives its own primary, sky-400,
 * which already clears it on the slate surfaces. Fills, borders, filled buttons and standalone icons keep
 * the primary. iOS's `CleansiaColors.primaryText` is the same pair.
 *
 * Picked from `surface`'s luminance so it follows the scheme actually in force, as
 * `CleansiaDestructiveButton` does.
 */
val ColorScheme.primaryText: Color
    get() = if (surface.luminance() < 0.5f) primary else PrimaryTextLight

/** [ColorScheme.primaryText] of the theme in force. */
@Composable
@ReadOnlyComposable
fun primaryText(): Color = MaterialTheme.colorScheme.primaryText
