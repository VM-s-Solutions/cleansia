package cz.cleansia.customer.ui.components

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.ui.theme.Sky600
import cz.cleansia.customer.ui.theme.Sky700
import cz.cleansia.customer.ui.theme.Sky950
import cz.cleansia.customer.ui.theme.Slate50
import cz.cleansia.customer.ui.theme.Slate900
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** The status-bar fade's colour, extent and icons, as iOS's `StatusBarFade` draws them. */
class StatusBarFadeTest {

    private val statusBar = 102f
    private val ease = 16f

    @Test
    fun `the colour is held at 90 percent behind the status bar, then eased out by its bottom`() {
        val stops = statusBarFadeStops(statusBar, ease, Sky950)
        val hold = (statusBar - ease) / statusBar

        assertEquals(0f, stops.first().first)
        assertEquals(0.9f, stops.first().second.alpha, ALPHA_STEP)
        stops.filter { it.first <= hold }.forEach { assertEquals(0.9f, it.second.alpha, ALPHA_STEP) }
        // It ends exactly at the status bar's bottom, clear: no tail below it.
        assertEquals(1f, stops.last().first)
        assertEquals(0f, stops.last().second.alpha, ALPHA_STEP)
        assertTrue("stops never run past the status bar", stops.all { it.first in 0f..1f })
    }

    @Test
    fun `the ease runs over iOS's last 5 points`() {
        assertEquals(5.dp, FadeEase)
    }

    @Test
    fun `the falloff is smooth and never steps back`() {
        val stops = statusBarFadeStops(statusBar, ease, Slate50)
        stops.toList().zipWithNext { a, b ->
            assertTrue("positions rise", b.first >= a.first)
            assertTrue("alpha only falls", b.second.alpha <= a.second.alpha + 0.0001f)
        }
        // Eight samples over the ease, so the eased curve shows no seam.
        assertEquals(10, stops.size)
    }

    /** Android interpolates a gradient unpremultiplied: a fade to transparent BLACK greys the light theme. */
    @Test
    fun `every stop is the one colour at some alpha, never transparent black`() {
        statusBarFadeStops(statusBar, ease, Slate50).forEach { (_, color) ->
            assertEquals(Slate50.copy(alpha = color.alpha), color)
        }
    }

    @Test
    fun `a status bar shorter than the ease eases over all of it`() {
        val stops = statusBarFadeStops(10f, ease, Slate50)
        assertEquals(0f, stops[1].first)
        assertEquals(0f, stops.last().second.alpha, ALPHA_STEP)
    }

    /** The Pixel 8 emulator: a 132 px status bar built around a camera hole whose bottom is at 102 px. */
    @Test
    fun `the fade ends at the camera hole's bottom, the line the clock is centred on`() {
        assertEquals(102, statusBarFadeHeight(statusBar = 132, cutout = 29.5f..102f))
        assertEquals(102, statusBarFadeHeight(statusBar = 132, cutout = 29.5f..101.4f))
    }

    @Test
    fun `without a cutout inside the status bar the fade ends at the status bar's bottom`() {
        assertEquals(63, statusBarFadeHeight(statusBar = 63, cutout = null))
        // An empty path, a cutout on the side in landscape, and one deeper than the status bar.
        assertEquals(63, statusBarFadeHeight(statusBar = 63, cutout = 0f..0f))
        assertEquals(63, statusBarFadeHeight(statusBar = 63, cutout = 1000f..1072f))
        assertEquals(63, statusBarFadeHeight(statusBar = 63, cutout = 0f..90f))
    }

    @Test
    fun `the hero's share is whole while it reaches the fade's bottom and none once it has passed`() {
        assertEquals(1f, statusBarFadeHeroShare(heroBottom = 600, height = 74))
        assertEquals(1f, statusBarFadeHeroShare(heroBottom = 74, height = 74))
        assertEquals(0.5f, statusBarFadeHeroShare(heroBottom = 37, height = 74), 0.001f)
        assertEquals(0f, statusBarFadeHeroShare(heroBottom = 0, height = 74))
        assertEquals(0f, statusBarFadeHeroShare(heroBottom = -400, height = 74))
        // No hero measured yet, and no status bar at all, are both safe.
        assertEquals(0f, statusBarFadeHeroShare(heroBottom = 0, height = 0))
    }

    @Test
    fun `the share moves one pixel at a time as the hero scrolls past`() {
        val shares = (100 downTo -10).map { statusBarFadeHeroShare(it, 74) }
        shares.zipWithNext { a, b ->
            assertTrue("monotonic", b <= a)
            assertTrue("no jump larger than one pixel's share", a - b <= 1f / 74 + 0.0001f)
        }
    }

    @Test
    fun `the colour is the hero's laid over the page in its share, or the page without a hero`() {
        assertEquals(Slate50, statusBarFadeColor(Slate50, heroTint = null, heroShare = 1f))
        assertEquals(Sky950, statusBarFadeColor(Slate50, Sky950, heroShare = 1f))
        assertEquals(Slate50, statusBarFadeColor(Slate50, Sky950, heroShare = 0f))
        assertEquals(Sky950.copy(alpha = 0.5f).compositeOver(Slate50), statusBarFadeColor(Slate50, Sky950, 0.5f))
    }

    @Test
    fun `the icons are light on the dark heroes and the dark page, dark on the light page`() {
        assertTrue(statusBarIconsLight(Sky950))
        assertTrue(statusBarIconsLight(Sky700))
        // The 60 % black icons read 3.2:1 on sky-600, white 4.1:1.
        assertTrue(statusBarIconsLight(Sky600))
        assertTrue(statusBarIconsLight(Slate900))
        assertFalse(statusBarIconsLight(Slate50))
        assertFalse(statusBarIconsLight(Color.White))
    }

    private companion object {
        /** An sRGB colour keeps its alpha in eight bits: 0.9 is stored as 230 / 255. */
        const val ALPHA_STEP = 1f / 255
    }
}
