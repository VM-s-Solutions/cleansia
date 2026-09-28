package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderAddress
import cz.cleansia.partner.api.model.OrderItem
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * A partner keeps the customer's name, address and phone while the job is live and for 24 hours after
 * completion; a cancellation closes them at once. After that the server sends its crew the browsing
 * shape, and the detail has to say the details were removed rather than promise them "once you take
 * the order".
 */
class PastJobDisclosureTest {

    private val street = OrderAddress(street = "Korunní 810/104", city = "Praha", zipCode = "12000")

    private fun crewOrder(status: Int, address: OrderAddress?) = OrderItem(
        orderStatus = Code(value = status),
        isAssignedToCurrentUser = true,
        address = address,
        customerName = if (address == null) "" else "Jana Nováková",
        customerPhone = if (address == null) "" else "+420600123456",
        customerAddressApproximate = "Praha · 120",
    )

    @Test
    fun `a completed job past its window says the details went 24 hours after completion`() {
        assertEquals(
            R.string.order_customer_details_closed_completed,
            crewOrder(COMPLETED, address = null).customerDetailsClosedNote(),
        )
    }

    @Test
    fun `a cancelled job says the details went with the cancellation`() {
        assertEquals(
            R.string.order_customer_details_closed_cancelled,
            crewOrder(CANCELLED, address = null).customerDetailsClosedNote(),
        )
    }

    @Test
    fun `a completed job inside its window still shows the customer`() {
        assertNull(crewOrder(COMPLETED, address = street).customerDetailsClosedNote())
    }

    @Test
    fun `a live job is never closed`() {
        listOf(CONFIRMED, ON_THE_WAY, IN_PROGRESS).forEach { status ->
            assertNull("status $status", crewOrder(status, address = street).customerDetailsClosedNote())
        }
    }

    @Test
    fun `a browsing partner never had the details, so nothing was removed`() {
        val browsing = OrderItem(
            orderStatus = Code(value = NEW),
            isAssignedToCurrentUser = false,
            address = null,
            customerName = "",
            customerAddressApproximate = "Praha · 120",
        )

        assertNull(browsing.customerDetailsClosedNote())
    }

    private companion object {
        const val NEW = 0
        const val CONFIRMED = 2
        const val ON_THE_WAY = 3
        const val IN_PROGRESS = 4
        const val COMPLETED = 5
        const val CANCELLED = 6
    }
}
