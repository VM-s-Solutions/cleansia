package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import java.time.Instant
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The server lets the crew leave a New or Confirmed job only, and the apps offer it there too. They are narrower in
 * two ways: before any lockout report, because dropping after "I cannot get in" gives up the lockout reward, and
 * before the job's start, because a job nobody has started by then is a no-show, which the administrators handle.
 */
class DropGateTest {

    private val start = Instant.parse("2026-10-25T09:00:00Z")
    private val anHourBefore = start.minusSeconds(3_600)

    private fun order(
        status: Int? = CONFIRMED,
        mine: Boolean? = true,
        lockoutReportedAt: String? = null,
        cleaningDateTime: String? = start.toString(),
    ) = OrderItem(
        orderStatus = status?.let { Code(value = it) },
        isAssignedToCurrentUser = mine,
        lockoutReportedAt = lockoutReportedAt,
        cleaningDateTime = cleaningDateTime,
    )

    @Test
    fun `the crew of a new or confirmed job can cancel it`() {
        assertTrue(order(status = NEW).droppable(anHourBefore))
        assertTrue(order(status = CONFIRMED).droppable(anHourBefore))
    }

    @Test
    fun `a job someone else is on offers nothing`() {
        assertFalse(order(mine = false).droppable(anHourBefore))
        assertFalse(order(mine = null).droppable(anHourBefore))
    }

    @Test
    fun `nothing is offered once the cleaner is on the way or working`() {
        listOf(PENDING, ON_THE_WAY, IN_PROGRESS, COMPLETED, CANCELLED, UNKNOWN).forEach { status ->
            assertFalse("status $status", order(status = status).droppable(anHourBefore))
        }
        assertFalse("no status on the wire", order(status = null).droppable(anHourBefore))
    }

    @Test
    fun `a reported lockout closes it`() {
        assertFalse(order(lockoutReportedAt = "2026-10-24T09:20:00Z").droppable(anHourBefore))
    }

    @Test
    fun `a blank lockout time is no report`() {
        assertTrue(order(lockoutReportedAt = "").droppable(anHourBefore))
        assertTrue(order(lockoutReportedAt = "  ").droppable(anHourBefore))
    }

    @Test
    fun `it is offered until the job's start and not from it`() {
        assertTrue("a minute before the start", order().droppable(start.minusSeconds(60)))
        assertFalse("at the start", order().droppable(start))
        assertFalse("a minute after the start", order().droppable(start.plusSeconds(60)))
    }

    @Test
    fun `without a start on the wire the status alone decides`() {
        assertTrue(order(cleaningDateTime = null).droppable(start.plusSeconds(86_400)))
        assertFalse(order(status = ON_THE_WAY, cleaningDateTime = null).droppable(anHourBefore))
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
