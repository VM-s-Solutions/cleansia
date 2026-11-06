package cz.cleansia.core.ui.components

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The day and time under an order's number are never shrunk to fit: a window that does not fit beside its day
 * starts the next line whole, without the separator.
 */
class OrderCalendarLineTest {

    @Test
    fun `a day and window that fit share one line, joined by the separator`() {
        assertEquals(listOf("Nov 24 · 08:00"), calendarLineRows("Nov 24", "08:00", split = false))
    }

    @Test
    fun `a window that does not fit beside its day starts the next line whole, without the separator`() {
        assertEquals(listOf("Thu, Nov 12", "14:00–16:00"), calendarLineRows("Thu, Nov 12", "14:00–16:00", split = true))
    }

    @Test
    fun `a day alone is one line, whatever its width`() {
        assertEquals(listOf("Nov 24"), calendarLineRows("Nov 24", null, split = true))
        assertEquals(listOf("Nov 24"), calendarLineRows("Nov 24", " ", split = true))
    }

    @Test
    fun `the line splits only when the joined line is wider than its room`() {
        assertFalse("a joined line exactly as wide as its room keeps one line", calendarLineSplits(hasWindow = true, joinedWidthPx = 200, roomPx = 200))
        assertTrue("one pixel more starts the window on the next line", calendarLineSplits(hasWindow = true, joinedWidthPx = 201, roomPx = 200))
        assertFalse("a day with no window never splits", calendarLineSplits(hasWindow = false, joinedWidthPx = 500, roomPx = 100))
        assertFalse("unbounded room never splits", calendarLineSplits(hasWindow = true, joinedWidthPx = 5_000, roomPx = Int.MAX_VALUE))
    }
}
