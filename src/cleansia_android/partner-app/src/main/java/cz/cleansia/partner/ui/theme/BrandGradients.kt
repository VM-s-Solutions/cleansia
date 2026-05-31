package cz.cleansia.partner.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.ui.graphics.Color
import cz.cleansia.partner.LocalAppSettings
import cz.cleansia.partner.core.settings.ThemePreference

/**
 * Brand gradients used by hero cards on Dashboard, Order details header,
 * Invoice total card, etc. Dark-mode pairs are muted heavily so large
 * gradient surfaces don't scream against the slate-900 background.
 *
 * Port of customer-app's BrandGradients but trimmed to what partner needs.
 */
object BrandGradients {

    private val BluePairLight = Sky600 to Sky400
    private val BluePairDark = Sky800 to Sky700

    private val TealPairLight = Color(0xFF0891B2) to Color(0xFF67E8F9)
    private val TealPairDark = Color(0xFF0E6E88) to Color(0xFF4BAEC1)

    private val GreenPairLight = Color(0xFF15803D) to Color(0xFF4ADE80)
    private val GreenPairDark = Color(0xFF166534) to Color(0xFF22C55E)

    @Composable @ReadOnlyComposable
    fun blue(): Pair<Color, Color> = if (isDark()) BluePairDark else BluePairLight

    @Composable @ReadOnlyComposable
    fun teal(): Pair<Color, Color> = if (isDark()) TealPairDark else TealPairLight

    @Composable @ReadOnlyComposable
    fun green(): Pair<Color, Color> = if (isDark()) GreenPairDark else GreenPairLight
}

@Composable @ReadOnlyComposable
fun Pair<Color, Color>.asList(): List<Color> = listOf(first, second)

@Composable @ReadOnlyComposable
fun isDark(): Boolean {
    val pref = LocalAppSettings.current.theme
    return when (pref) {
        ThemePreference.System -> isSystemInDarkTheme()
        ThemePreference.Light -> false
        ThemePreference.Dark -> true
    }
}
