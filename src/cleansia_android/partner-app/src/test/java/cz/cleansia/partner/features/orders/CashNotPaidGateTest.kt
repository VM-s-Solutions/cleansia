package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Mirrors `ReportCashNotPaid`'s gate: the cleaner on a cash job that is in progress, with the payment
 * still pending. Everything else the server would refuse, so the action is not offered.
 */
class CashNotPaidGateTest {

    private fun order(
        status: Int = IN_PROGRESS,
        paymentType: Int = CASH,
        paymentStatus: Int = PENDING,
        mine: Boolean? = true,
    ) = OrderItem(
        orderStatus = Code(value = status),
        paymentType = Code(value = paymentType),
        paymentStatus = Code(value = paymentStatus),
        isAssignedToCurrentUser = mine,
    )

    @Test
    fun `the cleaner on an in-progress cash job with the payment pending can report it`() {
        assertTrue(order().cashNotPaidReportable())
    }

    @Test
    fun `a job someone else is on offers nothing`() {
        assertFalse(order(mine = false).cashNotPaidReportable())
        assertFalse(order(mine = null).cashNotPaidReportable())
    }

    @Test
    fun `only an in-progress job can be reported`() {
        listOf(CONFIRMED, ON_THE_WAY, COMPLETED, CANCELLED).forEach { status ->
            assertFalse("status $status", order(status = status).cashNotPaidReportable())
        }
    }

    @Test
    fun `a card job never offers it`() {
        assertFalse(order(paymentType = CARD).cashNotPaidReportable())
    }

    @Test
    fun `recorded cash, a refund or a dispute is not a debt to report`() {
        listOf(PAID, FAILED, REFUNDED, DISPUTED, PARTIALLY_REFUNDED).forEach { paymentStatus ->
            assertFalse("payment status $paymentStatus", order(paymentStatus = paymentStatus).cashNotPaidReportable())
        }
    }

    @Test
    fun `the amount owed is the price less the credit applied`() {
        assertEquals(900.0, OrderItem(totalPrice = 1200.0, creditAppliedAmount = 300.0).cashNotPaidOwed()!!, 0.0)
        assertEquals(1200.0, OrderItem(totalPrice = 1200.0, creditAppliedAmount = 0.0).cashNotPaidOwed()!!, 0.0)
    }

    @Test
    fun `a missing price or credit figure names no amount rather than a guessed one`() {
        assertNull(OrderItem(totalPrice = null, creditAppliedAmount = 0.0).cashNotPaidOwed())
        assertNull(OrderItem(totalPrice = 1200.0, creditAppliedAmount = null).cashNotPaidOwed())
    }

    private companion object {
        const val CONFIRMED = 2
        const val ON_THE_WAY = 3
        const val IN_PROGRESS = 4
        const val COMPLETED = 5
        const val CANCELLED = 6
        const val CASH = 1
        const val CARD = 2
        const val PENDING = 1
        const val PAID = 2
        const val FAILED = 3
        const val REFUNDED = 4
        const val DISPUTED = 5
        const val PARTIALLY_REFUNDED = 6
    }
}
