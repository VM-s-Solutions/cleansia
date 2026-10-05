package cz.cleansia.customer.features.profile

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import cz.cleansia.customer.ui.components.statusBarIconsLight
import cz.cleansia.customer.ui.theme.Sky400
import cz.cleansia.customer.ui.theme.Sky600
import cz.cleansia.customer.ui.theme.Sky700
import cz.cleansia.customer.ui.theme.Sky800
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The status bar sits on Profile's hero at rest, and on the fade in the hero's top colour once scrolled,
 * so its clock and icons must read at 4.5:1 there (U-6: white on the brand blue measured 3.85:1 at rest).
 */
class ProfileHeroClockTest {

    private val lightBrand = Sky600 to Sky400
    private val darkBrand = Sky800 to Sky700

    @Test
    fun `in light mode the hero starts at sky-700 and the brand blue is left alone`() {
        assertEquals(Sky700 to Sky400, profileHeroColors(dark = false, brand = lightBrand))
        assertEquals(darkBrand, profileHeroColors(dark = true, brand = darkBrand))
    }

    /**
     * The clock's lowest pixel is about 15 % of the way down the hero on the Pixel 8 emulator (81 of about 500 px);
     * 20 % leaves room for a taller status bar over a shorter hero.
     */
    @Test
    fun `the white clock and icons read at 4_5 to 1 over the hero's top in both themes`() {
        listOf(false to lightBrand, true to darkBrand).forEach { (dark, brand) ->
            val (top, bottom) = profileHeroColors(dark, brand)
            val atClock = mix(top, bottom, 0.2f)
            val theme = if (dark) "dark" else "light"
            assertTrue("$theme: the icons are not light over the hero", statusBarIconsLight(top))
            assertTrue("$theme: white on the hero's top is ${contrast(Color.White, top)}:1", contrast(Color.White, top) >= 4.5)
            assertTrue("$theme: white at the clock is ${contrast(Color.White, atClock)}:1", contrast(Color.White, atClock) >= 4.5)
        }
    }

    @Test
    fun `the hero and its fade both take Profile's colours`() {
        val profile = File(moduleDir(), "src/main/java/cz/cleansia/customer/features/profile/ProfileTab.kt").readText()
        assertTrue(profile.contains("val heroColors = profileHeroColors(isDark(), BrandGradients.blue())"))
        assertTrue(profile.contains(".statusBarFade(scrollState, heroTint = heroColors.first, heroHeight = { heroHeight })"))
        assertTrue(profile.contains("Brush.verticalGradient(profileHeroColors(isDark(), BrandGradients.blue()).asList())"))
    }

    /** The gradient's colour [t] of the way from [a] to [b], as the shader interpolates it, in sRGB. */
    private fun mix(a: Color, b: Color, t: Float) =
        Color(a.red + (b.red - a.red) * t, a.green + (b.green - a.green) * t, a.blue + (b.blue - a.blue) * t)

    private fun contrast(a: Color, b: Color): Double {
        val (light, dark) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (light + 0.05) / (dark + 0.05)
    }

    private fun moduleDir(): File = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .firstOrNull { File(it, "src/main/java/cz/cleansia/customer").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")
}
