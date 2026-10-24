package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The server lets a cleaner drop out of a job at any status up to InProgress. The apps offer it more narrowly:
 * on the crew of a New or Confirmed job, before any lockout report, because once the cleaner has set off the
 * customer is already expecting them, and dropping after "I cannot get in" gives up the lockout reward.
 */
class DropGateTest {

    private fun order(
        status: Int? = CONFIRMED,
        mine: Boolean? = true,
        lockoutReportedAt: String? = null,
    ) = OrderItem(
        orderStatus = status?.let { Code(value = it) },
        isAssignedToCurrentUser = mine,
        lockoutReportedAt = lockoutReportedAt,
    )

    @Test
    fun `the crew of a new or confirmed job can cancel it`() {
        assertTrue(order(status = NEW).droppable())
        assertTrue(order(status = CONFIRMED).droppable())
    }

    @Test
    fun `a job someone else is on offers nothing`() {
        assertFalse(order(mine = false).droppable())
        assertFalse(order(mine = null).droppable())
    }

    @Test
    fun `nothing is offered once the cleaner is on the way or working`() {
        listOf(PENDING, ON_THE_WAY, IN_PROGRESS, COMPLETED, CANCELLED, UNKNOWN).forEach { status ->
            assertFalse("status $status", order(status = status).droppable())
        }
        assertFalse("no status on the wire", order(status = null).droppable())
    }

    @Test
    fun `a reported lockout closes it`() {
        assertFalse(order(lockoutReportedAt = "2026-10-24T09:20:00Z").droppable())
    }

    @Test
    fun `a blank lockout time is no report`() {
        assertTrue(order(lockoutReportedAt = "").droppable())
        assertTrue(order(lockoutReportedAt = "  ").droppable())
    }

    private companion object {
        const val NEW = 0
        const val PENDING = 1
        const val CONFIRMED = 2
        const val ON_THE_WAY = 3
        const val IN_PROGRESS = 4
        const val COMPLETED = 5
        const val CANCELLED = 6
        const val UNKNOWN = 42
    }
}
