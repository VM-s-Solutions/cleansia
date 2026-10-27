package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.AssignedEmployeeDto
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The crew close as the footer reads it: on a job with two or more cleaners each one marks their own
 * part done, and Complete comes back only once every seat still on the job is marked; a job with one
 * cleaner on it completes as it always has.
 */
class CompletionStepTest {

    private val me = "employee-me"
    private val mate = "employee-mate"
    private val marked = "2026-10-27T10:15:00Z"

    private fun seat(employeeId: String, doneAt: String? = null) = AssignedEmployeeDto(
        id = "seat-$employeeId",
        employeeId = employeeId,
        fullName = "Name",
        completionConfirmedAt = doneAt,
    )

    private fun order(vararg seats: AssignedEmployeeDto) = OrderItem(
        orderStatus = Code(value = 4),
        isAssignedToCurrentUser = true,
        assignedEmployees = seats.toList(),
    )

    @Test
    fun `a job with one cleaner on it completes in one step`() {
        assertEquals(CompletionStep.Complete, order(seat(me)).completionStep(me))
    }

    @Test
    fun `a job whose crew the wire did not carry completes as before`() {
        assertEquals(
            CompletionStep.Complete,
            OrderItem(orderStatus = Code(value = 4), assignedEmployees = null).completionStep(me),
        )
    }

    @Test
    fun `on a crew job nobody has marked, I mark my part`() {
        assertEquals(CompletionStep.MarkMyPart, order(seat(me), seat(mate)).completionStep(me))
    }

    @Test
    fun `when a crew mate has marked and I have not, I still mark my part`() {
        assertEquals(CompletionStep.MarkMyPart, order(seat(me), seat(mate, marked)).completionStep(me))
    }

    @Test
    fun `once I have marked and a crew mate has not, I wait for them`() {
        assertEquals(CompletionStep.WaitingForCrew, order(seat(me, marked), seat(mate)).completionStep(me))
    }

    @Test
    fun `once every seat is marked, any of the crew completes the job`() {
        assertEquals(CompletionStep.CompleteJob, order(seat(me, marked), seat(mate, marked)).completionStep(me))
        assertEquals(
            CompletionStep.CompleteJob,
            order(seat(me, marked), seat(mate, marked), seat("employee-third", marked)).completionStep(me),
        )
    }

    @Test
    fun `a blank stamp is no mark`() {
        assertEquals(CompletionStep.MarkMyPart, order(seat(me, " "), seat(mate, marked)).completionStep(me))
    }

    /** Marking again keeps the first mark, so offering it before the caller's id resolves costs nothing. */
    @Test
    fun `before my id resolves, an unfinished crew offers the mark`() {
        assertEquals(CompletionStep.MarkMyPart, order(seat(me, marked), seat(mate)).completionStep(null))
    }
}
