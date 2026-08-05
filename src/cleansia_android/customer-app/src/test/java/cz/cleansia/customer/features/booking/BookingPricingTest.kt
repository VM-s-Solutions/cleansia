package cz.cleansia.customer.features.booking

import kotlinx.datetime.Instant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The waiver is the server's verdict, carried in as an explicit argument so a call site that forgets
 * it does not compile. A screen that keeps charging a member the surcharge the server waived shows a
 * total the customer is never billed.
 */
class BookingPricingTest {

    private val now = Instant.parse("2026-08-05T10:00:00Z")
    private val expressSlot = Instant.parse("2026-08-05T13:00:00Z")
    private val standardSlot = Instant.parse("2026-08-05T18:00:00Z")

    @Test
    fun `an express slot needs the surcharge when nothing waives it`() {
        assertTrue(BookingPricing.requiresExpressSurcharge(expressSlot, waiverApplies = false, now = now))
    }

    @Test
    fun `an express slot needs no surcharge once the server waives it`() {
        assertFalse(BookingPricing.requiresExpressSurcharge(expressSlot, waiverApplies = true, now = now))
    }

    @Test
    fun `a standard slot never needs the surcharge`() {
        assertFalse(BookingPricing.requiresExpressSurcharge(standardSlot, waiverApplies = false, now = now))
    }

    @Test
    fun `a waived express slot adds no surcharge amount`() {
        assertEquals(
            0.0,
            BookingPricing.expressSurchargeAmount(1000.0, expressSlot, waiverApplies = true, now = now),
            0.001,
        )
    }

    @Test
    fun `an unwaived express slot adds the standard rate`() {
        assertEquals(
            200.0,
            BookingPricing.expressSurchargeAmount(1000.0, expressSlot, waiverApplies = false, now = now),
            0.001,
        )
    }

    @Test
    fun `the total drops the surcharge for a waived member`() {
        assertEquals(
            900.0,
            BookingPricing.finalTotal(
                basePrice = 1000.0,
                cleaningAt = expressSlot,
                tierDiscount = 0.0,
                promoDiscount = 100.0,
                waiverApplies = true,
                now = now,
            ),
            0.001,
        )
    }

    @Test
    fun `the total keeps the surcharge for everyone else`() {
        assertEquals(
            1080.0,
            BookingPricing.finalTotal(
                basePrice = 1000.0,
                cleaningAt = expressSlot,
                tierDiscount = 0.0,
                promoDiscount = 100.0,
                waiverApplies = false,
                now = now,
            ),
            0.001,
        )
    }

    @Test
    fun `a standard slot total is identical whether or not a waiver exists`() {
        val waived = BookingPricing.finalTotal(
            basePrice = 1000.0,
            cleaningAt = standardSlot,
            tierDiscount = 0.0,
            promoDiscount = 0.0,
            waiverApplies = true,
            now = now,
        )
        val charged = BookingPricing.finalTotal(
            basePrice = 1000.0,
            cleaningAt = standardSlot,
            tierDiscount = 0.0,
            promoDiscount = 0.0,
            waiverApplies = false,
            now = now,
        )
        assertEquals(1000.0, waived, 0.001)
        assertEquals(waived, charged, 0.001)
    }
}
