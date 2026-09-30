package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.booking.DirtinessLevel
import cz.cleansia.customer.core.orders.OrderDetailDto
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The order's subtotal is the charged total plus its discounts, so it already contains the stored
 * dirtiness surcharge. Drawn beside its own surcharge row it would count the surcharge twice.
 */
class OrderDirtinessBreakdownTest {

    /** 1000 of lines at the increased level: 300 surcharge, express 260 on the 1300, 120 off, 1440 charged. */
    @Test
    fun `the subtotal row leaves the surcharge to its own row so the rows add up to the total`() {
        val order = OrderDetailDto(
            id = "o-1",
            totalPrice = 1440.0,
            originalSubtotal = 1560.0,
            appliedDiscountSource = 2,
            membershipDiscountAmount = 120.0,
            dirtinessLevel = DirtinessLevel.Increased,
            dirtinessSurchargeAmount = 300.0,
        )

        assertEquals(1260.0, order.subtotalBeforeDirtiness(), 0.001)
        assertEquals(order.totalPrice, order.subtotalBeforeDirtiness() + 300.0 - 120.0, 0.001)
    }

    @Test
    fun `an order at the normal level keeps its whole subtotal`() {
        val order = OrderDetailDto(
            id = "o-1",
            totalPrice = 1000.0,
            originalSubtotal = 1100.0,
            appliedDiscountSource = 1,
        )

        assertEquals(1100.0, order.subtotalBeforeDirtiness(), 0.001)
    }
}
