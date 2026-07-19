package cz.cleansia.partner.features.orders

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Pins the prettified status labels to the sentence-case form both platforms
 * converge on ("On the way", not "On The Way") — iOS pins its side in
 * `OrderStatusLabelTests`; this is the Android twin, guarding the lowercasing
 * of camel-boundary words that the label previously dropped.
 */
class StatusTimelineLabelTest {

    @Test
    fun `camel-cased backend names render as sentence case`() {
        assertEquals("On the way", labelForStatusName("OnTheWay", null))
        assertEquals("In progress", labelForStatusName("InProgress", null))
    }

    @Test
    fun `single-word names pass through unchanged`() {
        assertEquals("Confirmed", labelForStatusName("Confirmed", null))
        assertEquals("Completed", labelForStatusName("Completed", null))
    }

    @Test
    fun `blank name falls back to the numeric status value`() {
        assertEquals("On the way", labelForStatusName(null, 3))
        assertEquals("In progress", labelForStatusName("", 4))
        assertEquals("—", labelForStatusName(null, 99))
    }
}
