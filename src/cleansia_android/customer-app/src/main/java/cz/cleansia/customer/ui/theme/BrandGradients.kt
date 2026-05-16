package cz.cleansia.customer.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.ui.graphics.Color
import cz.cleansia.customer.LocalAppSettings
import cz.cleansia.customer.core.settings.ThemePreference

/**
 * Centralised brand gradient pairs. Light-mode uses vivid brand hues; dark-mode
 * returns muted variants so large gradient surfaces don't scream against the
 * slate-900 background.
 *
 * Blue is muted heavily (primary — everywhere, the biggest offender).
 * Purple / cyan are muted lightly since they're accent-only (one hero slide,
 * one featured package) and shouldn't lose their identity.
 */
object BrandGradients {

    // ── Blue (primary) ──
    private val BluePairLight = Sky600 to Sky400
    private val BluePairDark = Sky800 to Sky700 // heavily muted

    // ── Purple (accent) ──
    private val PurplePairLight = Color(0xFF7C3AED) to Color(0xFFA78BFA)
    private val PurplePairDark = Color(0xFF5B2AB0) to Color(0xFF7C5ABF) // lightly muted

    // ── Cyan (accent) ──
    private val CyanPairLight = Color(0xFF0891B2) to Color(0xFF67E8F9)
    private val CyanPairDark = Color(0xFF0E6E88) to Color(0xFF4BAEC1) // lightly muted

    @Composable @ReadOnlyComposable
    fun blue(): Pair<Color, Color> = if (isDark()) BluePairDark else BluePairLight

    @Composable @ReadOnlyComposable
    fun purple(): Pair<Color, Color> = if (isDark()) PurplePairDark else PurplePairLight

    @Composable @ReadOnlyComposable
    fun cyan(): Pair<Color, Color> = if (isDark()) CyanPairDark else CyanPairLight
}

/** Helpers to resolve gradient pairs as a plain [List] for `Brush.*Gradient(colors = ...)`. */
@Composable @ReadOnlyComposable
fun Pair<Color, Color>.asList(): List<Color> = listOf(first, second)

/**
 * Resolves the effective dark-mode state, honouring the user's explicit theme
 * override from AppSettings (System / Light / Dark).
 */
@Composable @ReadOnlyComposable
fun isDark(): Boolean {
    val pref = LocalAppSettings.current.theme
    return when (pref) {
        ThemePreference.System -> isSystemInDarkTheme()
        ThemePreference.Light -> false
        ThemePreference.Dark -> true
    }
}

/**
 * Background tint for "selected" UI states (service cards, address rows,
 * payment-method rows). In light mode we want the familiar pale Sky-100 fill.
 * In dark mode we use a low-alpha primary overlay so the selection reads as
 * "tinted" without blaring bright blue against a slate background.
 */
@Composable @ReadOnlyComposable
fun selectionTint(): androidx.compose.ui.graphics.Color =
    if (isDark()) {
        // primary@18% over the surface reads as a subtle highlight in dark mode
        androidx.compose.material3.MaterialTheme.colorScheme.primary.copy(alpha = 0.18f)
    } else {
        Sky100
    }
