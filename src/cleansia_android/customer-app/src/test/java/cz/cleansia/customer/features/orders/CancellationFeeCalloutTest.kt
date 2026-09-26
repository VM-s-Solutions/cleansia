package cz.cleansia.customer.features.orders

import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.CancellationFeePreviewDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The sheet's fee copy is chosen by the server's tier discriminator and nothing
 * else. Every case here would also pass if the resolver re-derived the tier from
 * `feeRate` — except [rate_disagreeing_with_the_tier_does_not_move_the_copy],
 * which is the one that fails the moment someone reintroduces arithmetic.
 */
class CancellationFeeCalloutTest {

    private fun preview(
        tier: Int?,
        feeRate: Double = 0.0,
        feeAmount: Double = 0.0,
        refundAmount: Double = 0.0,
        expressWaiverForfeited: Boolean = false,
        graceMinutes: Int? = null,
    ) = CancellationFeePreviewDto(
        orderId = "order-1",
        tier = tier,
        feeRate = feeRate,
        feeAmount = feeAmount,
        refundAmount = refundAmount,
        totalPrice = 1000.0,
        currencyCode = "CZK",
        expressWaiverForfeitedOnCancel = expressWaiverForfeited,
        oopsWindowMinutes = graceMinutes,
    )

    @Test
    fun `every tier gets its own sentence`() {
        val titles = (0..4).map { cancellationFeeCallout(preview(tier = it))!!.titleRes }

        assertEquals("each tier must read differently to the customer", 5, titles.toSet().size)
        assertEquals(R.string.order_cancel_fee_not_accepted, titles[0])
        assertEquals(R.string.order_cancel_fee_oops, titles[1])
        assertEquals(R.string.order_cancel_fee_outside_window, titles[2])
        assertEquals(R.string.order_cancel_fee_partial, titles[3])
        assertEquals(R.string.order_cancel_fee_last_minute, titles[4])
    }

    @Test
    fun `the three free tiers state no fee and carry no money`() {
        listOf(0, 1, 2).forEach { tier ->
            val callout = cancellationFeeCallout(preview(tier = tier))!!
            assertEquals(CancellationFeeSeverity.Free, callout.severity)
            assertEquals(R.string.order_cancel_fee_none, callout.amountRes)
            assertTrue("a free tier has no amounts to show", callout.amounts.isEmpty())
        }
    }

    @Test
    fun `a partial cancellation states the fee and the refund in that order`() {
        val callout = cancellationFeeCallout(
            preview(tier = 3, feeRate = 0.25, feeAmount = 250.0, refundAmount = 750.0),
        )!!

        assertEquals(R.string.order_cancel_fee_split, callout.amountRes)
        assertEquals(listOf(250.0, 750.0), callout.amounts)
        assertEquals(CancellationFeeSeverity.Fee, callout.severity)
    }

    @Test
    fun `a last-minute cancellation still states the refund`() {
        val callout = cancellationFeeCallout(
            preview(tier = 4, feeRate = 0.5, feeAmount = 500.0, refundAmount = 500.0),
        )!!

        // The old copy claimed "no refund is available this close to the
        // cleaning" while the server refunded half. The refund is never hidden.
        assertEquals(R.string.order_cancel_fee_split, callout.amountRes)
        assertEquals(listOf(500.0, 500.0), callout.amounts)
        assertEquals(CancellationFeeSeverity.LastMinute, callout.severity)
    }

    @Test
    fun `guest charged tiers identify policy refund as an estimate without changing signed in copy`() {
        for (tier in listOf(3, 4)) {
            val quote = preview(tier = tier, feeAmount = 22.5, refundAmount = 67.5)
            val guest = cancellationFeeCallout(quote, refundIsEstimate = true)!!
            assertEquals(R.string.guest_order_fee_estimate, guest.amountRes)
            assertEquals(listOf(22.5, 67.5), guest.amounts)
            assertEquals(R.string.order_cancel_fee_split, cancellationFeeCallout(quote)!!.amountRes)
        }
    }

    @Test
    fun `rate disagreeing with the tier does not move the copy`() {
        val callout = cancellationFeeCallout(
            preview(tier = 2, feeRate = 0.5, feeAmount = 500.0, refundAmount = 500.0),
        )!!

        assertEquals(R.string.order_cancel_fee_outside_window, callout.titleRes)
        assertEquals(CancellationFeeSeverity.Free, callout.severity)
        assertTrue(callout.amounts.isEmpty())
    }

    @Test
    fun `an unknown tier says nothing rather than guessing`() {
        assertNull(cancellationFeeCallout(preview(tier = null)))
        assertNull(cancellationFeeCallout(preview(tier = 99)))
    }

    @Test
    fun `the express-waiver warning rides every tier including the free ones`() {
        (0..4).forEach { tier ->
            val callout = cancellationFeeCallout(preview(tier = tier, expressWaiverForfeited = true))!!
            assertTrue(
                "tier $tier hides the forfeited express booking",
                callout.warnsExpressWaiverForfeited,
            )
        }
    }

    @Test
    fun `no warning when the server says nothing is forfeited`() {
        (0..4).forEach { tier ->
            assertTrue(!cancellationFeeCallout(preview(tier = tier))!!.warnsExpressWaiverForfeited)
        }
    }

    /** The grace is the customer's own, so the sheet states the server's figure on every tier. */
    @Test
    fun `the server's grace rides every tier unchanged`() {
        listOf(15, 60).forEach { minutes ->
            (0..4).forEach { tier ->
                assertEquals(
                    "tier $tier restated the grace",
                    minutes,
                    cancellationFeeCallout(preview(tier = tier, graceMinutes = minutes))!!.graceMinutes,
                )
            }
        }
    }

    @Test
    fun `no grace from the server states none`() {
        (0..4).forEach { tier ->
            assertNull(cancellationFeeCallout(preview(tier = tier))!!.graceMinutes)
        }
    }
}
