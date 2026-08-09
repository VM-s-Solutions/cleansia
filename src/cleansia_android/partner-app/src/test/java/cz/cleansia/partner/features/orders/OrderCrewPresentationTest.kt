package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderItem
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * `TakeOrder`'s free-seat conjunct refuses a take the detail gave no warning
 * about, and neither the crew size nor the seat count is derivable client-side.
 *
 * Every fixture here is arranged **multi-seat and partially crewed** and every
 * expectation is a literal: four of the five wire members are numeric and the
 * fifth is a boolean, so an unpopulated fixture yields exactly the `0`/`false`
 * a lazy assertion is satisfied by.
 */
class OrderCrewPresentationTest {

    /** 2 required, 2 max, 1 assigned ⇒ 1 available. */
    private val partiallyCrewed = OrderItem(
        requiredEmployees = 2,
        maxEmployees = 2,
        assignedEmployeesCount = 1,
        availableSpots = 1,
        hasAvailableSpots = true,
    )

    @Test
    fun `a partially crewed order names the crew and the open seat`() {
        val crew = partiallyCrewed.orderCrew()
        assertEquals(OrderCrew.SpotsOpen(crewSize = 2, openSpots = 1), crew)
        assertEquals(2, crew?.crewSize)
    }

    @Test
    fun `a full crew warns that no seat is left`() {
        val crew = partiallyCrewed.copy(
            assignedEmployeesCount = 2,
            availableSpots = 0,
            hasAvailableSpots = false,
        ).orderCrew()
        assertEquals(OrderCrew.Full(crewSize = 2), crew)
        assertEquals(2, crew?.crewSize)
    }

    /**
     * The server owns the seat arithmetic and sends both shapes; the client
     * reads them and computes neither. When they disagree the flag — the same
     * one behind the take's refusal — decides.
     */
    @Test
    fun `the server's flag decides, not the count`() {
        assertEquals(
            OrderCrew.Full(crewSize = 2),
            partiallyCrewed.copy(hasAvailableSpots = false).orderCrew(),
        )
        assertEquals(
            OrderCrew.Full(crewSize = 2),
            partiallyCrewed.copy(availableSpots = 0).orderCrew(),
        )
    }

    @Test
    fun `a solo job is still reported`() {
        val crew = OrderItem(
            requiredEmployees = 1,
            maxEmployees = 1,
            assignedEmployeesCount = 1,
            availableSpots = 0,
            hasAvailableSpots = false,
        ).orderCrew()
        assertEquals(OrderCrew.Full(crewSize = 1), crew)
    }

    /** A build talking to a server that predates the seat block says nothing. */
    @Test
    fun `an absent crew size renders no seat facts`() {
        assertNull(OrderItem(availableSpots = 1, hasAvailableSpots = true).orderCrew())
        assertNull(OrderItem(requiredEmployees = 0).orderCrew())
    }
}
