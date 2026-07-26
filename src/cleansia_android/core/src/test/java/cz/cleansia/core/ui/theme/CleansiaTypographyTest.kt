package cz.cleansia.core.ui.theme

import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pure-JVM pins for [CleansiaTypography].
 *
 * A `TextStyle` cannot be *rendered* without an instrumented or screenshot harness, and this repo
 * has neither — but the values themselves are plain data, so the two invariants the four newly
 * defined slots depend on are cheap to guard here. Both tests exist because of a specific call
 * site, not for coverage: Android CI runs only `compileDebugKotlin` + `testDebugUnitTest`, so a
 * silent regression in this file would otherwise ship.
 */
class CleansiaTypographyTest {

    private val brandFamilies: Set<FontFamily?> = setOf(Poppins, Nunito)

    private val allSlots: List<Pair<String, TextStyle>> = listOf(
        "displayLarge" to CleansiaTypography.displayLarge,
        "displayMedium" to CleansiaTypography.displayMedium,
        "displaySmall" to CleansiaTypography.displaySmall,
        "headlineLarge" to CleansiaTypography.headlineLarge,
        "headlineMedium" to CleansiaTypography.headlineMedium,
        "headlineSmall" to CleansiaTypography.headlineSmall,
        "titleLarge" to CleansiaTypography.titleLarge,
        "titleMedium" to CleansiaTypography.titleMedium,
        "titleSmall" to CleansiaTypography.titleSmall,
        "bodyLarge" to CleansiaTypography.bodyLarge,
        "bodyMedium" to CleansiaTypography.bodyMedium,
        "bodySmall" to CleansiaTypography.bodySmall,
        "labelLarge" to CleansiaTypography.labelLarge,
        "labelMedium" to CleansiaTypography.labelMedium,
        "labelSmall" to CleansiaTypography.labelSmall,
    )

    /**
     * Call-site pin for partner `OnboardingScreen.kt` (the hero title).
     *
     * That title used to read `typography.displaySmall` back when the slot was undefined, so it
     * rendered M3's un-rescaled 36sp **Roboto** baseline — a size this ramp never offered. Any slot
     * left out of [CleansiaTypography] silently falls back to `FontFamily.Default` (Roboto) on
     * every device, GMS or not, and nothing in the toolchain complains. This is the assertion that
     * complains.
     */
    @Test
    fun `every M3 slot is defined on a brand family — an undefined slot silently renders Roboto`() {
        val offenders = allSlots.filterNot { (_, style) -> style.fontFamily in brandFamilies }
        assertTrue(
            "These typography slots do not resolve to Poppins or Nunito, so they render the " +
                "platform default (Roboto): ${offenders.map { it.first }}",
            offenders.isEmpty(),
        )
    }

    /**
     * Call-site pin for the two avatar-initial glyphs — customer `EditProfileScreen.kt` and partner
     * `PersonalSectionScreen.kt`. Both sit inside a fixed 104.dp circle and both pin
     * `fontSize = 36.sp` on top of `displaySmall`, precisely because the slot itself is 26sp.
     *
     * If someone "simplifies" those call sites by dropping the pin, or raises this slot back
     * towards 36sp, one of the two breaks. Keeping 26sp asserted here documents why the pin is
     * there so the next reader does not remove it as redundant.
     */
    @Test
    fun `displaySmall is 26sp, which is why the avatar initials pin their own font size`() {
        assertEquals(26.sp, CleansiaTypography.displaySmall.fontSize)
        assertEquals(Poppins, CleansiaTypography.displaySmall.fontFamily)
        assertEquals(FontWeight.Bold, CleansiaTypography.displaySmall.fontWeight)
    }

    /**
     * Metric pin for the three slots chosen to be reflow-free plus the one that is not.
     * titleSmall (14/20) and bodySmall (12/16) deliberately match the M3 defaults they replace, so
     * 179 of the 234 previously-Roboto call sites change typeface with zero layout shift.
     * labelMedium (13/18, up from 12/16) is the only slot in this commit that reflows.
     */
    @Test
    fun `the four completed slots keep their agreed metrics`() {
        assertEquals(34.sp, CleansiaTypography.displaySmall.lineHeight)

        assertEquals(14.sp, CleansiaTypography.titleSmall.fontSize)
        assertEquals(20.sp, CleansiaTypography.titleSmall.lineHeight)

        assertEquals(12.sp, CleansiaTypography.bodySmall.fontSize)
        assertEquals(16.sp, CleansiaTypography.bodySmall.lineHeight)
        assertEquals(FontWeight.Normal, CleansiaTypography.bodySmall.fontWeight)

        assertEquals(13.sp, CleansiaTypography.labelMedium.fontSize)
        assertEquals(18.sp, CleansiaTypography.labelMedium.lineHeight)
    }

    /**
     * Nunito ships no Medium(500) in the bundle, so Compose's CSS-style matcher resolves a Medium
     * request down to Regular(400). The three Nunito slots added here therefore ask for Bold or
     * Normal — "correcting" them to Medium for M3 fidelity would render Regular and make the whole
     * change invisible.
     */
    @Test
    fun `the new Nunito slots avoid the weight Nunito does not ship`() {
        listOf(
            "titleSmall" to CleansiaTypography.titleSmall,
            "bodySmall" to CleansiaTypography.bodySmall,
            "labelMedium" to CleansiaTypography.labelMedium,
        ).forEach { (name, style) ->
            assertEquals(Nunito, style.fontFamily)
            assertTrue(
                "$name asks for FontWeight.Medium, which Nunito does not ship — it will render " +
                    "Regular and the style will look unchanged",
                style.fontWeight != FontWeight.Medium,
            )
        }
    }
}
