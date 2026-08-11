package cz.cleansia.core.ui.theme

import androidx.compose.ui.graphics.Color

/**
 * Brand-identity colours that are the same in both apps and in both schemes, so
 * they cannot come from `MaterialTheme.colorScheme`: the launch gradient always
 * reads white-on-sky regardless of the device theme, exactly as the iOS
 * `CleansiaColors.splashGradientStart/End` pair does.
 */
val SplashGradientStart = Color(0xFF0284C7) // sky-600
val SplashGradientEnd = Color(0xFF38BDF8) // sky-400
