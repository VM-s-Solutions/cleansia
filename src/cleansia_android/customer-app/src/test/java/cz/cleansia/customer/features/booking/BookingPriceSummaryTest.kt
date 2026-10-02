package cz.cleansia.customer.features.booking

import cz.cleansia.customer.core.booking.QuoteOrderResponse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
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

    /** `CreateOrder.Handler`'s own base: `calc.TotalPrice - calc.ExpressSurchargeAmount`. */
    @Test
    fun `the pre-surcharge subtotal is the gross less the server's own surcharge amount`() {
        assertEquals(
            1000.0,
            quote(totalPrice = 1200.0, surchargeApplied = true, surcharge = 200.0).preSurchargeSubtotal,
            0.001,
        )
    }

    @Test
    fun `without a surcharge the pre-surcharge subtotal is the whole quote`() {
        assertEquals(1000.0, quote(totalPrice = 1000.0).preSurchargeSubtotal, 0.001)
    }

    /**
     * The surcharge here is deliberately not 20 % of anything: only the quote's own ratio takes 100 to
     * 115, so a restatement hardcoding the express rate fails this and a passing one owns no rate.
     */
    @Test
    fun `restating a discount uses the quote's own ratio, never the express rate`() {
        assertEquals(
            115.0,
            quote(totalPrice = 1150.0, surchargeApplied = true, surcharge = 150.0).discountAsCharged(100.0),
            0.001,
        )
    }

    /** The customer would have paid 1200 and pays (1000 - 100) * 1.2 = 1080, so they saved 120. */
    @Test
    fun `a discount resolved on the express base is restated against the charged price`() {
        val quote = quote(totalPrice = 1200.0, surchargeApplied = true, surcharge = 200.0)

        assertEquals(120.0, quote.discountAsCharged(100.0), 0.001)
        assertEquals(1080.0, BookingPriceSummary.resolve(quote, quote.discountAsCharged(100.0)).total, 0.001)
    }

    @Test
    fun `without a surcharge a discount is already stated against the charged price`() {
        assertEquals(100.0, quote(totalPrice = 1000.0).discountAsCharged(100.0), 0.001)
    }

    /** A waived slot is charged no surcharge, so it is the plain case however express the hour is. */
    @Test
    fun `a waived express slot leaves the discount alone`() {
        assertEquals(
            100.0,
            quote(totalPrice = 1000.0, surchargeApplied = true, surcharge = 0.0, waived = true)
                .discountAsCharged(100.0),
            0.001,
        )
    }

    /**
     * 1000 of lines at the increased level: 300 surcharge inside the 1300 base, express 260 on top, and a
     * 120 discount stated against the charged 1560. The subtotal row is the lines alone, so the four rows
     * add up to what the order is created with.
     */
    @Test
    fun `the dirtiness surcharge is its own row and the rows add up to the total`() {
        val summary = BookingPriceSummary.resolve(
            quote(totalPrice = 1560.0, surchargeApplied = true, surcharge = 260.0, dirtiness = 300.0),
            discount = 120.0,
        )

        assertEquals(1000.0, summary.subtotal, 0.001)
        assertEquals(300.0, summary.dirtinessSurcharge, 0.001)
        assertEquals(260.0, summary.expressSurcharge, 0.001)
        assertEquals(1440.0, summary.total, 0.001)
        assertEquals(
            summary.total,
            summary.subtotal + summary.dirtinessSurcharge + summary.expressSurcharge - 120.0,
            0.001,
        )
    }

    /** The server resolves discounts and tier floors on this base, surcharge included, so it keeps it. */
    @Test
    fun `the pre-surcharge subtotal keeps the dirtiness surcharge inside it`() {
        assertEquals(
            1300.0,
            quote(totalPrice = 1560.0, surchargeApplied = true, surcharge = 260.0, dirtiness = 300.0)
                .preSurchargeSubtotal,
            0.001,
        )
    }

    @Test
    fun `an empty base cannot scale anything and returns the discount unchanged`() {
        assertEquals(50.0, quote(totalPrice = 0.0).discountAsCharged(50.0), 0.001)
    }

    // ── credit: the client preview of BookingPolicy.CapCreditForOrder ──
    //
    // The vectors are the server's own (CreditAtCheckoutTests) at the 70 % share the wire carries; the
    // iOS twin runs the same ones, so the four copies of the formula cannot drift apart unnoticed.

    @Test
    fun `the cap spends the whole balance when it fits under the share`() {
        assertEquals(500.0, capCreditForOrder(balance = 500.0, charged = 2000.0, share = 0.7), 0.0)
    }

    @Test
    fun `the cap stops at the share when the balance is larger`() {
        assertEquals(700.0, capCreditForOrder(balance = 2000.0, charged = 1000.0, share = 0.7), 0.0)
        assertEquals(700.0, capCreditForOrder(balance = 700.0, charged = 1000.0, share = 0.7), 0.0)
    }

    /** 70 % of 33.33 is 23.331, and Stripe takes whole cents. */
    @Test
    fun `the cap floors to whole cents`() {
        assertEquals(23.33, capCreditForOrder(balance = 1000.0, charged = 33.33, share = 0.7), 0.0)
    }

    /** 100.10 × 0.7 is 70.07 in decimal and 70.0699… in binary floating point, which floors a cent short. */
    @Test
    fun `the cap is decimal arithmetic, so a price binary floats cannot represent keeps its cent`() {
        assertEquals(70.07, capCreditForOrder(balance = 1000.0, charged = 100.10, share = 0.7), 0.0)
    }

    @Test
    fun `the card always pays something`() {
        listOf(1.0, 7.0, 99.99, 1000.0, 13333.33).forEach { price ->
            val applied = capCreditForOrder(balance = 1_000_000.0, charged = price, share = 0.7)
            assertTrue("$applied of credit would settle all of a $price order", applied < price)
        }
    }

    @Test
    fun `no balance, no order, no share or negative nonsense spends nothing`() {
        assertEquals(0.0, capCreditForOrder(balance = 0.0, charged = 1000.0, share = 0.7), 0.0)
        assertEquals(0.0, capCreditForOrder(balance = 500.0, charged = 0.0, share = 0.7), 0.0)
        assertEquals(0.0, capCreditForOrder(balance = -100.0, charged = 1000.0, share = 0.7), 0.0)
        assertEquals(0.0, capCreditForOrder(balance = 500.0, charged = 1000.0, share = 0.0), 0.0)
    }

    @Test
    fun `a card booking takes the credit off what the card is asked for, not off the total`() {
        val summary = BookingPriceSummary.resolve(
            quote(totalPrice = 1000.0, creditBalance = 250.0, creditShare = 0.7),
            discount = 0.0,
            payByCard = true,
        )

        assertEquals(1000.0, summary.total, 0.001)
        assertEquals(250.0, summary.creditApplied, 0.001)
        assertEquals(750.0, summary.dueOnCard, 0.001)
    }

    /** Credit is capped on the price being charged, promo included, as the server caps the order's. */
    @Test
    fun `the cap is taken on the discounted total`() {
        val summary = BookingPriceSummary.resolve(
            quote(totalPrice = 1000.0, creditBalance = 5000.0, creditShare = 0.7),
            discount = 200.0,
            payByCard = true,
        )

        assertEquals(560.0, summary.creditApplied, 0.001)
        assertEquals(240.0, summary.dueOnCard, 0.001)
    }

    @Test
    fun `cash takes no credit, so what is due is the whole total`() {
        val summary = BookingPriceSummary.resolve(
            quote(totalPrice = 1000.0, creditBalance = 250.0, creditShare = 0.7),
            discount = 0.0,
            payByCard = false,
        )

        assertEquals(0.0, summary.creditApplied, 0.0)
        assertEquals(1000.0, summary.dueOnCard, 0.001)
    }

    private fun quote(
        totalPrice: Double,
        surchargeApplied: Boolean = false,
        surcharge: Double = 0.0,
        waived: Boolean = false,
        dirtiness: Double = 0.0,
        creditBalance: Double = 0.0,
        creditShare: Double = 0.0,
    ) = QuoteOrderResponse(
        requiredEmployees = 1,
        finalPriceAfterDiscount = 0.0,
        originalSubtotal = 0.0,
        appliedDiscountSource = 0,
        extrasSubtotal = 0.0,
        totalPrice = totalPrice,
        currencyId = "cur-1",
        currencyCode = "CZK",
        servicesSubtotal = 0.0,
        packagesSubtotal = 0.0,
        expressSurchargeApplied = surchargeApplied,
        expressSurchargeAmount = surcharge,
        expressSurchargeWaivedByMembership = waived,
        dirtinessSurchargeAmount = dirtiness,
        creditBalance = creditBalance,
        creditMaxShareOfOrder = creditShare,
    )
}
