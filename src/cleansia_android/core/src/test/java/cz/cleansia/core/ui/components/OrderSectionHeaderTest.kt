package cz.cleansia.core.ui.components

import androidx.compose.ui.unit.Constraints
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A section title's count is never squeezed beside its title: when the two do not fit one line, the count starts the
 * next line whole, without the separator, as the calendar line's window does.
 */
class OrderSectionHeaderTest {

    @Test
    fun `beside its title the count follows the separator, on its own line it drops it`() {
        assertEquals("· 3 jobs", sectionSubtitleText("3 jobs", stacked = false))
        assertEquals("3 jobs", sectionSubtitleText("3 jobs", stacked = true))
    }

    @Test
    fun `the count leaves the title's line only when the two are wider than their room`() {
        assertFalse("exactly as wide as the room keeps one line", sectionSubtitleStacks(titleWidthPx = 180, gapPx = 18, subtitleWidthPx = 102, roomPx = 300))
        assertTrue("one pixel more starts the count on the next line", sectionSubtitleStacks(titleWidthPx = 180, gapPx = 18, subtitleWidthPx = 103, roomPx = 300))
        assertFalse("unbounded room never stacks", sectionSubtitleStacks(titleWidthPx = 5_000, gapPx = 18, subtitleWidthPx = 5_000, roomPx = Int.MAX_VALUE))
    }

    @Test
    fun `beside a title with unbounded room the count's room stays unbounded, so a parent may ask the header's size`() {
        val unbounded = sectionSubtitleRoomPx(roomPx = Constraints.Infinity, titleWidthPx = 180, gapPx = 18)
        assertEquals(Constraints.Infinity, unbounded)
        assertEquals(Constraints.Infinity, Constraints(maxWidth = unbounded).maxWidth)
        assertEquals(102, sectionSubtitleRoomPx(roomPx = 300, titleWidthPx = 180, gapPx = 18))
        assertEquals(0, sectionSubtitleRoomPx(roomPx = 150, titleWidthPx = 180, gapPx = 18))
    }
}
