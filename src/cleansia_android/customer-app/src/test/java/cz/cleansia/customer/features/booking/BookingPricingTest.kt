package cz.cleansia.customer.features.booking

import kotlinx.datetime.Instant
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Slot-grid tagging only. Nothing here is money — that lives in [BookingPriceSummary], which reads the
 * server's own figures instead of deriving any from these bands.
 */
class BookingPricingTest {

    private val now = Instant.parse("2026-08-05T10:00:00Z")

    @Test
    fun `a slot inside the express band is tagged express`() {
        assertTrue(BookingPricing.requiresExpressSurcharge(now.plusHours(3), now))
    }

    @Test
    fun `the express band is closed at its lower bound`() {
        assertTrue(BookingPricing.requiresExpressSurcharge(now.plusHours(2), now))
    }

    @Test
    fun `the express band is open at its upper bound`() {
        assertFalse(BookingPricing.requiresExpressSurcharge(now.plusHours(4), now))
    }

    @Test
    fun `a slot beyond the standard lead time is not express`() {
        assertFalse(BookingPricing.requiresExpressSurcharge(now.plusHours(8), now))
    }

    @Test
    fun `a slot below the express lead time is not express`() {
        assertFalse(BookingPricing.requiresExpressSurcharge(now.plusHours(1), now))
    }

    @Test
    fun `no slot picked yet is not express`() {
        assertFalse(BookingPricing.requiresExpressSurcharge(null, now))
    }

    private fun Instant.plusHours(hours: Long): Instant =
        Instant.fromEpochMilliseconds(toEpochMilliseconds() + hours * 60 * 60 * 1000)
}
