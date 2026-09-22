package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.orders.AssignedEmployeeDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.WorkContractAcceptanceDto
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The acceptance carries no name and the crew entry carries no acceptance; the line the customer
 * reads is the pairing of the two by the seat id, and every way the pairing can fail must drop the
 * line rather than name the wrong cleaner or a cleaner for nothing.
 */
class WorkContractAcceptanceLineTest {

    private fun seat(id: String, employeeId: String, name: String?) =
        AssignedEmployeeDto(id = id, employeeId = employeeId, fullName = name, phoneNumber = null)

    private fun acceptance(
        id: String,
        seatId: String,
        acceptedOn: String = "2026-08-10T18:40:00Z",
        version: String? = "2026-09-20",
    ) = WorkContractAcceptanceDto(
        id = id,
        orderEmployeeId = seatId,
        employeeId = "emp-$seatId",
        acceptedOn = acceptedOn,
        documentVersion = version,
        language = "cs",
    )

    private fun order(
        crew: List<AssignedEmployeeDto>?,
        acceptances: List<WorkContractAcceptanceDto>?,
    ) = OrderDetailDto(
        id = "order-1",
        totalPrice = 4380.0,
        originalSubtotal = 3650.0,
        appliedDiscountSource = 0,
        assignedEmployees = crew,
        workContractAcceptances = acceptances,
    )

    @Test
    fun `one acceptance on one seat is one line naming that seat's cleaner`() {
        val lines = order(
            crew = listOf(seat("seat-1", "emp-1", "Jana")),
            acceptances = listOf(acceptance("acc-1", "seat-1")),
        ).workContractAcceptanceLines()

        assertEquals(
            listOf(WorkContractAcceptanceLine("acc-1", "Jana", "2026-08-10T18:40:00Z", "2026-09-20")),
            lines,
        )
    }

    @Test
    fun `two crew members each with an acceptance are two lines in the server's order`() {
        val lines = order(
            crew = listOf(seat("seat-1", "emp-1", "Jana"), seat("seat-2", "emp-2", "Petr")),
            acceptances = listOf(acceptance("acc-2", "seat-2"), acceptance("acc-1", "seat-1")),
        ).workContractAcceptanceLines()

        assertEquals(listOf("acc-2" to "Petr", "acc-1" to "Jana"), lines.map { it.id to it.cleanerName })
    }

    @Test
    fun `no acceptance is no line even with a crew on the job`() {
        val crew = listOf(seat("seat-1", "emp-1", "Jana"))

        assertEquals(emptyList<WorkContractAcceptanceLine>(), order(crew, acceptances = null).workContractAcceptanceLines())
        assertEquals(emptyList<WorkContractAcceptanceLine>(), order(crew, acceptances = emptyList()).workContractAcceptanceLines())
    }

    @Test
    fun `an acceptance naming no current seat is dropped rather than shown nameless`() {
        val lines = order(
            crew = listOf(seat("seat-1", "emp-1", "Jana")),
            acceptances = listOf(acceptance("acc-9", "seat-gone"), acceptance("acc-1", "seat-1")),
        ).workContractAcceptanceLines()

        assertEquals(listOf("acc-1"), lines.map { it.id })
    }

    @Test
    fun `an acceptance with no id or no instant is dropped rather than rendered unreadable`() {
        val lines = order(
            crew = listOf(seat("seat-1", "emp-1", "Jana"), seat("seat-2", "emp-2", "Petr")),
            acceptances = listOf(
                acceptance("acc-1", "seat-1").copy(id = null),
                acceptance("acc-2", "seat-2").copy(acceptedOn = null),
            ),
        ).workContractAcceptanceLines()

        assertEquals(emptyList<WorkContractAcceptanceLine>(), lines)
    }

    @Test
    fun `a blank crew name and a missing version read as absent, not as text`() {
        val line = order(
            crew = listOf(seat("seat-1", "emp-1", "  ")),
            acceptances = listOf(acceptance("acc-1", "seat-1", version = null)),
        ).workContractAcceptanceLines().single()

        assertEquals(null, line.cleanerName)
        assertEquals("", line.documentVersion)
    }
}
