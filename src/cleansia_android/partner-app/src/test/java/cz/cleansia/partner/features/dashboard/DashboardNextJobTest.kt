package cz.cleansia.partner.features.dashboard

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderListItem
import java.time.Instant
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.ZoneId
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** The rules behind the dashboard's next-job card and its greeting's today line. */
class DashboardNextJobTest {

    private val prague = ZoneId.of("Europe/Prague")

    private fun at(local: String): Instant = LocalDateTime.parse(local).atZone(prague).toInstant()

    private fun order(id: String, status: Int, startsAt: String?) =
        OrderListItem(id = id, orderStatus = Code(value = status), cleaningDateTime = startsAt)

    @Test
    fun `tomorrow is the next calendar day, not anything 24 to 48 hours away`() {
        val mondayNight = at("2026-10-12T21:00")
        assertFalse("Wednesday 20:00 is 47 h away and two midnights on", startsTomorrow(at("2026-10-14T20:00"), mondayNight, prague))
        assertTrue("Tuesday 22:00 is 25 h away, on the next day", startsTomorrow(at("2026-10-13T22:00"), mondayNight, prague))
        assertTrue(
            "Tuesday 23:00 from just after Monday's midnight",
            startsTomorrow(at("2026-10-13T23:00"), at("2026-10-12T00:30"), prague),
        )
    }

    @Test
    fun `an undated active order never becomes the next job`() {
        val undated = order("undated", status = 2, startsAt = null)
        val dated = order("dated", status = 2, startsAt = "2026-10-13T08:00:00Z")
        assertEquals("dated", pickNextJob(listOf(undated, dated))?.id)
        assertNull(pickNextJob(listOf(undated, order("blank", status = 3, startsAt = ""))))
    }

    @Test
    fun `the next job is the soonest confirmed, on-the-way or in-progress order`() {
        val upcoming = listOf(
            order("later", status = 2, startsAt = "2026-10-14T08:00:00Z"),
            order("completed", status = 5, startsAt = "2026-10-12T08:00:00Z"),
            order("soonest", status = 3, startsAt = "2026-10-13T08:00:00Z"),
        )
        assertEquals("soonest", pickNextJob(upcoming)?.id)
    }

    @Test
    fun `the today count is unknown until the upcoming list has been read`() {
        val monday = LocalDate.of(2026, 10, 12)
        assertNull("an unread list must not read as a free day", todaysJobCount(null, monday, prague))
        assertEquals(0, todaysJobCount(emptyList(), monday, prague))
        val upcoming = listOf(
            order("today", status = 2, startsAt = "2026-10-12T08:00:00Z"),
            order("tomorrow", status = 2, startsAt = "2026-10-13T08:00:00Z"),
            order("undated", status = 2, startsAt = null),
        )
        assertEquals(1, todaysJobCount(upcoming, monday, prague))
    }
}
