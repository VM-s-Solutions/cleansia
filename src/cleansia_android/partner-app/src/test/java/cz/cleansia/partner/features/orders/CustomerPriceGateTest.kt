package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * On a cash job the customer's price is the amount the cleaner collects at the door; on any other job
 * the cleaner's figure is their own pay, and the customer's price is not theirs to see.
 */
class CustomerPriceGateTest {

    private fun order(paymentType: Int?) = OrderItem(
        paymentType = paymentType?.let { Code(value = it) },
        needsConfirmation = false,
    )

    @Test
    fun `a cash job shows the customer's price`() {
        assertTrue(order(CASH).showsCustomerPrice())
    }

    @Test
    fun `a card job shows no customer price`() {
        assertFalse(order(CARD).showsCustomerPrice())
    }

    @Test
    fun `a job whose payment type did not arrive shows no customer price`() {
        assertFalse(order(paymentType = null).showsCustomerPrice())
        assertFalse(OrderItem(paymentType = Code(), needsConfirmation = false).showsCustomerPrice())
    }

    private companion object {
        const val CASH = 1
        const val CARD = 2
    }
}
