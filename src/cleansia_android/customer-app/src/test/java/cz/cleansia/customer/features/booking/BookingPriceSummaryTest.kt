package cz.cleansia.customer.features.booking

import cz.cleansia.customer.core.booking.QuoteOrderResponse
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The screen used to re-apply +20 % on top of a server total that already contained it, so an express
 * booking displayed roughly a fifth more than the order was created with. The rule these tests pin is
 * that no money on this screen is ever computed from a rate — it is split out of the server's figures.
 */
class BookingPriceSummaryTest {

    @Test
    fun `no quote yet resolves to zeros rather than a guess`() {
        assertEquals(
            BookingPriceSummary(0.0, 0.0, BookingPriceSummary.ExpressLine.NotExpress, 0.0),
            BookingPriceSummary.resolve(quote = null, discount = 0.0),
        )
    }

    @Test
    fun `a standard slot shows the whole quote as subtotal`() {
        val summary = BookingPriceSummary.resolve(quote(totalPrice = 1000.0), discount = 0.0)

        assertEquals(1000.0, summary.subtotal, 0.001)
        assertEquals(0.0, summary.expressSurcharge, 0.001)
        assertEquals(BookingPriceSummary.ExpressLine.NotExpress, summary.expressLine)
        assertEquals(1000.0, summary.total, 0.001)
    }

    /**
     * The old shape returned 1320 here — it treated the gross 1200 as a pre-surcharge base, discounted
     * it to 1100 and added 20 % of that again. The server charges 1100.
     */
    @Test
    fun `an express total is the server total less the discount, never a rate on top`() {
        val summary = BookingPriceSummary.resolve(
            quote(totalPrice = 1200.0, surchargeApplied = true, surcharge = 200.0),
            discount = 100.0,
        )

        assertEquals(1000.0, summary.subtotal, 0.001)
        assertEquals(200.0, summary.expressSurcharge, 0.001)
        assertEquals(BookingPriceSummary.ExpressLine.Charged, summary.expressLine)
        assertEquals(1100.0, summary.total, 0.001)
    }

    /**
     * The surcharge here is deliberately NOT 20 % of anything the client could derive: not of the
     * pre-discount subtotal (200), not of the post-discount one (210). Only reading the server's own
     * `expressSurchargeAmount` produces 150, so this one assertion rejects **both** candidate bases the
     * client used to choose between — the pre-/post-discount question has no client-side answer left.
     */
    @Test
    fun `the surcharge row is the server amount, not a rate over any client-chosen base`() {
        val summary = BookingPriceSummary.resolve(
            quote(totalPrice = 1150.0, surchargeApplied = true, surcharge = 150.0),
            discount = 100.0,
        )

        assertEquals(150.0, summary.expressSurcharge, 0.001)
        assertEquals(1000.0, summary.subtotal, 0.001)
        assertEquals(1050.0, summary.total, 0.001)
    }

    @Test
    fun `a waived express slot carries no surcharge and the total the server charges`() {
        val summary = BookingPriceSummary.resolve(
            quote(totalPrice = 1000.0, waived = true),
            discount = 50.0,
        )

        assertEquals(1000.0, summary.subtotal, 0.001)
        assertEquals(0.0, summary.expressSurcharge, 0.001)
        assertEquals(BookingPriceSummary.ExpressLine.Waived, summary.expressLine)
        assertEquals(950.0, summary.total, 0.001)
    }

    /** The waived verdict outranks the charged one; the server never sets both, but the order is fixed. */
    @Test
    fun `waived outranks charged`() {
        assertEquals(
            BookingPriceSummary.ExpressLine.Waived,
            BookingPriceSummary.resolve(
                quote(totalPrice = 1000.0, surchargeApplied = true, surcharge = 200.0, waived = true),
                discount = 0.0,
            ).expressLine,
        )
    }

    @Test
    fun `a discount larger than the order never renders a negative total`() {
        assertEquals(
            0.0,
            BookingPriceSummary.resolve(quote(totalPrice = 100.0), discount = 500.0).total,
            0.001,
        )
    }

    private fun quote(
        totalPrice: Double,
        surchargeApplied: Boolean = false,
        surcharge: Double = 0.0,
        waived: Boolean = false,
    ) = QuoteOrderResponse(
        totalPrice = totalPrice,
        currencyId = "cur-1",
        currencyCode = "CZK",
        servicesSubtotal = 0.0,
        packagesSubtotal = 0.0,
        expressSurchargeApplied = surchargeApplied,
        expressSurchargeAmount = surcharge,
        expressSurchargeWaivedByMembership = waived,
        exchangeRate = 1.0,
    )
}
