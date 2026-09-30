package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The customer's confirm of a recurring cash occurrence no longer marks it Paid, so it reaches the
 * door owing cash like a one-off booking and the partner records it the same way.
 */
class CashCollectionGateTest {

    private fun order(paymentType: Int, paymentStatus: Int, recurringTemplateId: String? = null) = OrderItem(
        paymentType = Code(value = paymentType),
        paymentStatus = Code(value = paymentStatus),
        recurringTemplateId = recurringTemplateId,
        needsConfirmation = false,
    )

    @Test
    fun `a confirmed recurring cash occurrence asks for the cash`() {
        assertTrue(order(CASH, PENDING, recurringTemplateId = "tpl-1").needsCashCollection())
    }

    @Test
    fun `a one-off cash booking asks for the cash`() {
        assertTrue(order(CASH, PENDING).needsCashCollection())
    }

    @Test
    fun `recorded cash is Paid and asks for nothing`() {
        assertFalse(order(CASH, PAID, recurringTemplateId = "tpl-1").needsCashCollection())
        assertFalse(order(CASH, PAID).needsCashCollection())
    }

    @Test
    fun `a card order never asks for cash`() {
        assertFalse(order(CARD, PENDING, recurringTemplateId = "tpl-1").needsCashCollection())
        assertFalse(order(CARD, PENDING).needsCashCollection())
    }

    private companion object {
        const val CASH = 1
        const val CARD = 2
        const val PENDING = 1
        const val PAID = 2
    }
}
