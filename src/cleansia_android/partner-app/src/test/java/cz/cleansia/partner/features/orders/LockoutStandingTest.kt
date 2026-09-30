package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import java.time.Instant
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * `ReportOrderLockout` answers only the crew of an order that is Confirmed, on the way or in progress,
 * and refuses it before the booked start plus `BookingPolicy.LockoutWaitMinutes`.
 */
class LockoutStandingTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private val start = Instant.parse("2026-09-29T10:00:00Z")

    private fun order(
        status: Int = 4,
        mine: Boolean = true,
        cleaningDateTime: String? = "2026-09-29T10:00:00Z",
        lockoutReportedAt: String? = null,
        lockoutCallAttempts: String? = null,
    ) = OrderItem(
        orderStatus = Code(value = status),
        isAssignedToCurrentUser = mine,
        cleaningDateTime = cleaningDateTime,
        lockoutReportedAt = lockoutReportedAt,
        lockoutCallAttempts = lockoutCallAttempts,
    )

    @Test
    fun `the report opens the wait after the booked start and not a moment sooner`() {
        val opensAt = start.plusSeconds(LOCKOUT_WAIT_MINUTES * 60)

        assertEquals(LockoutStanding.NotYet(opensAt), order().lockoutStanding(opensAt.minusSeconds(1)))
        assertEquals(LockoutStanding.Open, order().lockoutStanding(opensAt))
    }

    @Test
    fun `a cleaner waiting at the door has the wall clock re-read every thirty seconds, not once at the end`() {
        val opensAt = start.plusSeconds(LOCKOUT_WAIT_MINUTES * 60)

        assertEquals(30_000L, lockoutClockStep(start, opensAt))
        assertEquals(5_000L, lockoutClockStep(opensAt.minusSeconds(5), opensAt))
        assertEquals(0L, lockoutClockStep(opensAt.plusSeconds(300), opensAt))
    }

    @Test
    fun `the wait is the fifteen minutes the server holds the cleaner to`() {
        assertEquals(15L, LOCKOUT_WAIT_MINUTES)
    }

    @Test
    fun `a crew member may report from Confirmed through InProgress`() {
        val late = start.plusSeconds(3600)

        assertEquals(
            listOf(2, 3, 4),
            (0..6).filter { order(status = it).lockoutStanding(late) == LockoutStanding.Open },
        )
    }

    @Test
    fun `a cleaner who is not on the crew is offered nothing`() {
        assertEquals(LockoutStanding.Hidden, order(mine = false).lockoutStanding(start.plusSeconds(3600)))
    }

    @Test
    fun `a job with no readable start is offered nothing rather than an ungated report`() {
        assertEquals(LockoutStanding.Hidden, order(cleaningDateTime = null).lockoutStanding(start.plusSeconds(3600)))
        assertEquals(LockoutStanding.Hidden, order(cleaningDateTime = "soon").lockoutStanding(start.plusSeconds(3600)))
    }

    @Test
    fun `a report already made is shown as made, with the calls it names`() {
        val detail = json.decodeFromString(
            OrderItem.serializer(),
            """
            {
              "id": "ord-1",
              "orderStatus": { "value": 3 },
              "isAssignedToCurrentUser": true,
              "cleaningDateTime": "2026-09-29T10:00:00Z",
              "lockoutReportedAt": "2026-09-29T10:20:00Z",
              "lockoutCallAttempts": "Called at 10:05 and 10:15, no answer"
            }
            """.trimIndent(),
        )

        assertEquals(
            LockoutStanding.Reported("2026-09-29T10:20:00Z", "Called at 10:05 and 10:15, no answer"),
            detail.lockoutStanding(start.plusSeconds(3600)),
        )
    }

    @Test
    fun `a report on a job that has since closed is not offered again`() {
        val reported = order(status = 6, lockoutReportedAt = "2026-09-29T10:20:00Z")

        assertEquals(LockoutStanding.Hidden, reported.lockoutStanding(start.plusSeconds(3600)))
    }
}
