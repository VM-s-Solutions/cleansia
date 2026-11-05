package cz.cleansia.customer.features.booking

import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.ui.components.MascotFigureBox
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Both arts draw on a 360 px canvas: the cleaning still covers rows 43-314 (272 tall), the welcoming art the hero's
 * box, rows 51-299 (249 tall).
 */
class MascotHandOverTest {

    private val cleaning = MascotFigureBox.Cleaning
    private val welcoming = MascotFigureBox.Welcoming

    /** The canvas the confirmation reports for a figure [figureHeight] tall whose box stands at [left], [top]. */
    private fun heroCanvas(figureHeight: Float, left: Float, top: Float): Rect {
        val k = figureHeight / 249f
        return Rect(Offset(left - 88f * k, top - 51f * k), Size(360f * k, 360f * k))
    }

    private val phoneHero = heroCanvas(figureHeight = 348f, left = 855f, top = 228f)
    private val smallPhoneHero = heroCanvas(figureHeight = 192f, left = 790f, top = 246f)

    private fun near(expected: Float, actual: Float, what: String = "") = assertEquals(what, expected, actual, 0.01f)

    @Test
    fun `the busy box draws the cleaner as tall as the hero's figure`() {
        near(153.53f, cleaning.canvasSide(figureHeight = 116f))
        near(84.71f, cleaning.canvasSide(figureHeight = 64f))
        near(105.78f, cleaning.figureHeight(canvasSide = 140f))
        near(348f, welcoming.figureHeight(canvasSide = phoneHero.height))
        near(192f, welcoming.figureHeight(canvasSide = smallPhoneHero.height))
    }

    @Test
    fun `the welcoming box is the hero's canvas`() {
        for (height in listOf(64f, 71.7f, 96f, 116f, 136f)) {
            val hero = heroCanvas(height, left = 0f, top = 0f)
            near(hero.width, welcoming.canvasSide(height), "at $height")
            near(height, welcoming.figureHeight(hero.height), "at $height")
        }
    }

    @Test
    fun `a stand-in stands as tall as the figure it replaces, on the same line and the same vertical`() {
        for (hero in listOf(phoneHero, smallPhoneHero)) {
            val cleaner = cleaning.canvas(standingAs = welcoming, on = hero)
            near(cleaner.width, cleaner.height)
            near(welcoming.figureHeight(hero.height), cleaning.figureHeight(cleaner.height))
            near(hero.top + hero.height * 51f / 360f, cleaner.top + cleaner.height * 43f / 360f)
            near(hero.center.x, cleaner.center.x)
            val back = welcoming.canvas(standingAs = cleaning, on = cleaner)
            near(hero.left, back.left)
            near(hero.top, back.top)
            near(hero.width, back.width)
        }
    }

    @Test
    fun `from a busy card sized to the hero, both stand-ins keep the hero's height on one line all the way`() {
        val heroFigure = welcoming.figureHeight(phoneHero.height)
        val side = cleaning.canvasSide(heroFigure)
        val busy = Rect(Offset(618f - side / 2, 1_180f), Size(side, side))
        for (step in 0..10) {
            val eased = step / 10f
            val (cleaner, welcomer) = flightCanvases(from = busy, to = phoneHero, eased = eased)
            near(heroFigure, cleaning.figureHeight(cleaner.height), "cleaner at $eased")
            near(heroFigure, welcoming.figureHeight(welcomer.height), "welcomer at $eased")
            near(cleaner.top + cleaner.height * 43f / 360f, welcomer.top + welcomer.height * 51f / 360f, "top line at $eased")
            near(cleaner.center.x, welcomer.center.x, "vertical at $eased")
        }
        assertEquals(busy, flightCanvases(busy, phoneHero, 0f).first)
        assertEquals(phoneHero, flightCanvases(busy, phoneHero, 1f).second)
    }

    @Test
    fun `from a busy card of another size, the two poses still blend at one height`() {
        val busy = Rect(Offset(408f, 1_100f), Size(420f, 420f))
        for (step in 0..10) {
            val eased = step / 10f
            val (cleaner, welcomer) = flightCanvases(from = busy, to = smallPhoneHero, eased = eased)
            near(cleaning.figureHeight(cleaner.height), welcoming.figureHeight(welcomer.height), "at $eased")
        }
    }

    @Test
    fun `before the hero is laid out, its height comes from the size tier of the confirmation's room`() {
        assertEquals(88.dp, 640.dp - confirmationRoom(height = 640.dp, navInset = 0.dp))
        assertEquals(136.dp, 640.dp - confirmationRoom(height = 640.dp, navInset = 48.dp))
        assertEquals(64.dp, expectedFigureHeight(height = 647.dp, navInset = 0.dp, fontScale = 1f))
        assertEquals(116.dp, expectedFigureHeight(height = 648.dp, navInset = 0.dp, fontScale = 1f))
        assertEquals(64.dp, expectedFigureHeight(height = 695.dp, navInset = 48.dp, fontScale = 1f))
        assertEquals(116.dp, expectedFigureHeight(height = 787.dp, navInset = 0.dp, fontScale = 1f))
        assertEquals(136.dp, expectedFigureHeight(height = 788.dp, navInset = 0.dp, fontScale = 1f))
        assertEquals(96.dp, expectedFigureHeight(height = 788.dp, navInset = 0.dp, fontScale = 1.5f))
        assertEquals(96.dp, expectedFigureHeight(height = 600.dp, navInset = 0.dp, fontScale = 1.5f))
    }
}
