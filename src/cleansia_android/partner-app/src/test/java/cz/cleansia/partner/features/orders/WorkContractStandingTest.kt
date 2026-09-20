package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.AssignedEmployeeDto
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.WorkContractAcceptanceDto
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The pairing rule: the caller's acceptance is the row whose seat is their own crew entry. Pairing by
 * employee id directly would be wrong on a re-take — the dropped seat's row names the same employee
 * and a different seat — and pairing by "any row exists" would show one crew member the other's.
 */
class WorkContractStandingTest {

    private val me = "employee-me"
    private val mate = "employee-mate"

    private fun seat(id: String, employeeId: String) =
        AssignedEmployeeDto(id = id, employeeId = employeeId, fullName = "Name")

    private fun row(id: String, seatId: String, employeeId: String) = WorkContractAcceptanceDto(
        id = id,
        orderEmployeeId = seatId,
        employeeId = employeeId,
        acceptedOn = "2026-08-10T18:40:00Z",
        documentVersion = "2026-09-20",
        language = "cs",
    )

    private fun order(
        status: Int = 2,
        seats: List<AssignedEmployeeDto> = emptyList(),
        rows: List<WorkContractAcceptanceDto> = emptyList(),
    ) = OrderItem(
        orderStatus = Code(value = status),
        assignedEmployees = seats,
        workContractAcceptances = rows,
        isAssignedToCurrentUser = seats.any { it.employeeId == me },
    )

    @Test
    fun `my seat with my row is accepted, keyed on the acceptance`() {
        val standing = order(
            seats = listOf(seat("seat-mate", mate), seat("seat-me", me)),
            rows = listOf(row("acc-mate", "seat-mate", mate), row("acc-me", "seat-me", me)),
        ).workContractStanding(me)

        assertEquals(WorkContractStanding.Accepted("acc-me", "2026-08-10T18:40:00Z", "2026-09-20"), standing)
    }

    @Test
    fun `my seat with no row on a live job is pending`() {
        val standing = order(
            seats = listOf(seat("seat-mate", mate), seat("seat-me", me)),
            rows = listOf(row("acc-mate", "seat-mate", mate)),
        ).workContractStanding(me)

        assertEquals(WorkContractStanding.Pending, standing)
    }

    @Test
    fun `a crew mate's row is never mine`() {
        val standing = order(
            seats = listOf(seat("seat-me", me)),
            rows = listOf(row("acc-other", "seat-other", me)),
        ).workContractStanding(me)

        assertEquals(WorkContractStanding.Pending, standing)
    }

    @Test
    fun `not on the crew is nothing, whatever the rows say`() {
        val standing = order(
            seats = listOf(seat("seat-mate", mate)),
            rows = listOf(row("acc-mate", "seat-mate", mate)),
        ).workContractStanding(me)

        assertEquals(WorkContractStanding.None, standing)
    }

    @Test
    fun `an unresolved employee id pairs nothing`() {
        val standing = order(seats = listOf(seat("seat-me", me))).workContractStanding(null)

        assertEquals(WorkContractStanding.None, standing)
    }

    /** The standalone acceptance is refused on a job that is over, so the banner is not offered there. */
    @Test
    fun `a seat with no row on a finished or cancelled job is not pending`() {
        listOf(5, 6).forEach { status ->
            val standing = order(status = status, seats = listOf(seat("seat-me", me))).workContractStanding(me)
            assertEquals("status $status", WorkContractStanding.None, standing)
        }
    }

    @Test
    fun `a seat with no row stays pending through the whole live lifecycle`() {
        listOf(0, 2, 3, 4).forEach { status ->
            val standing = order(status = status, seats = listOf(seat("seat-me", me))).workContractStanding(me)
            assertEquals("status $status", WorkContractStanding.Pending, standing)
        }
    }

    @Test
    fun `an accepted row on a finished job still reads as accepted`() {
        val standing = order(
            status = 5,
            seats = listOf(seat("seat-me", me)),
            rows = listOf(row("acc-me", "seat-me", me)),
        ).workContractStanding(me)

        assertEquals(WorkContractStanding.Accepted("acc-me", "2026-08-10T18:40:00Z", "2026-09-20"), standing)
    }
}
