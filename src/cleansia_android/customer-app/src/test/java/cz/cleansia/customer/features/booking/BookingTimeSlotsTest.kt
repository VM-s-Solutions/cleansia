package cz.cleansia.customer.features.booking

import kotlinx.datetime.Instant
import kotlinx.datetime.LocalDate
import kotlinx.datetime.TimeZone
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class BookingTimeSlotsTest {
    private val today = LocalDate(2026, 9, 10)
    private val now = Instant.parse("2026-09-10T10:15:00Z")

    @Test
    fun `future date offers each quarter hour until the daily closing bound`() {
        val slots = timeSlotsFor(LocalDate(2026, 9, 11), now, TimeZone.UTC)

        assertEquals(48, slots.size)
        assertEquals("08:00", slots.first().time)
        assertEquals("19:45", slots.last().time)
        assertEquals(listOf("10:00", "10:15", "10:30", "10:45"), slots.filter { it.time.startsWith("10:") }.map { it.time })
        assertEquals(slots.size, slots.map { it.time }.toSet().size)
        assertTrue(slots.all { it.state == SlotState.Available })
    }

    @Test
    fun `quarter hours keep the two hour lead time and four hour express boundary`() {
        val slots = timeSlotsFor(today, now, TimeZone.UTC).associate { it.time to it.state }

        assertEquals(SlotState.Unavailable, slots["12:00"])
        assertEquals(SlotState.Express, slots["12:15"])
        assertEquals(SlotState.Express, slots["14:00"])
        assertEquals(SlotState.Available, slots["14:15"])
        assertEquals(SlotState.Available, slots["14:30"])
    }

    @Test
    fun `a slot just inside the lead time is unavailable`() {
        val slots = timeSlotsFor(today, Instant.parse("2026-09-10T10:15:01Z"), TimeZone.UTC).associate { it.time to it.state }

        assertEquals(SlotState.Unavailable, slots["12:15"])
        assertEquals(SlotState.Express, slots["12:30"])
    }

    // The part of day before the arrival times — the web wizard's dayParts (/customer-app/ordering-flow).

    @Test
    fun `every arrival time sits in exactly one part, sixteen to a part`() {
        val slots = timeSlotsFor(LocalDate(2026, 9, 11), now, TimeZone.UTC)
        val parts = groupByDayPart(slots)

        assertEquals(listOf(DayPart.Morning, DayPart.Afternoon, DayPart.Evening), parts.map { it.part })
        assertEquals(slots.map { it.time }, parts.flatMap { part -> part.slots.map { it.time } })
        assertEquals(listOf(16, 16, 16), parts.map { it.slots.size })
        assertEquals(
            listOf("08:00" to "11:45", "12:00" to "15:45", "16:00" to "19:45"),
            parts.map { it.slots.first().time to it.slots.last().time },
        )
    }

    @Test
    fun `a future day opens on the part that holds the booked time, else on the morning`() {
        val parts = groupByDayPart(timeSlotsFor(LocalDate(2026, 9, 11), now, TimeZone.UTC))

        assertEquals(DayPart.Morning, openingDayPart(parts, ""))
        assertEquals(DayPart.Morning, openingDayPart(parts, "09:00"))
        assertEquals(DayPart.Afternoon, openingDayPart(parts, "12:00"))
        assertEquals(DayPart.Afternoon, openingDayPart(parts, "15:45"))
        assertEquals(DayPart.Evening, openingDayPart(parts, "16:00"))
    }

    /** The web's own case: at 13:00 today the morning is closed and the first bookable slot is 15:00. */
    @Test
    fun `at 13 00 today the morning is disabled and the step opens on the afternoon`() {
        val parts = groupByDayPart(timeSlotsFor(today, Instant.parse("2026-09-10T13:00:00Z"), TimeZone.UTC))

        assertEquals(listOf(false, true, true), parts.map { it.bookable })
        assertEquals(0, parts[0].bookableCount)
        assertEquals(4, parts[1].bookableCount)
        assertEquals("15:00", parts[1].slots.first { it.state != SlotState.Unavailable }.time)
        assertEquals(DayPart.Afternoon, openingDayPart(parts, ""))
        // A booked time the lead time has since overtaken does not hold the step on a closed part.
        assertEquals(DayPart.Afternoon, openingDayPart(parts, "09:00"))
    }

    @Test
    fun `a time outside the window belongs to no part`() {
        assertEquals(null, DayPart.of("07:45"))
        assertEquals(null, DayPart.of("20:00"))
        assertEquals(null, DayPart.of(""))
        assertEquals(DayPart.Evening, DayPart.of("19:45"))
    }

    @Test
    fun `selected quarter hour retains its minutes when converted to UTC`() {
        val prague = TimeZone.of("Europe/Prague")

        assertEquals(Instant.parse("2026-09-10T08:15:00Z"), combineDateAndTime(today, "10:15", prague))
        assertEquals(Instant.parse("2026-09-10T08:45:00Z"), combineDateAndTime(today, "10:45", prague))
    }

    // A resumed draft keeps its time only while the When step still offers it, in the band it was left in.

    private fun holds(date: LocalDate?, time: String, leftAt: String?, at: String) =
        draftTimeStillHolds(date, time, leftAt?.let(Instant::parse), Instant.parse(at), TimeZone.UTC)

    @Test
    fun `a resumed time the step still offers in the same band holds`() {
        assertTrue(holds(LocalDate(2026, 9, 11), "10:00", "2026-09-10T09:00:00Z", "2026-09-10T10:15:00Z"))
        assertTrue(holds(today, "15:00", "2026-09-10T09:00:00Z", "2026-09-10T10:15:00Z"))
    }

    @Test
    fun `a resumed time that came inside the lead time, or whose day has passed, no longer holds`() {
        assertFalse(holds(today, "12:00", "2026-09-10T07:00:00Z", "2026-09-10T10:15:00Z"))
        assertFalse(holds(today, "09:00", "2026-09-10T06:00:00Z", "2026-09-10T10:15:00Z"))
        assertFalse(holds(LocalDate(2026, 9, 9), "10:00", "2026-09-08T10:00:00Z", "2026-09-10T10:15:00Z"))
    }

    /** The draft was quoted in the band it was left in; a standard slot gone express would be charged more. */
    @Test
    fun `a standard slot that slid into the express band no longer holds, an express one left as express does`() {
        assertFalse(holds(today, "14:00", "2026-09-10T09:00:00Z", "2026-09-10T10:15:00Z"))
        assertTrue(holds(today, "13:00", "2026-09-10T10:00:00Z", "2026-09-10T10:15:00Z"))
    }

    @Test
    fun `tomorrow's slot left the night before holds while standard and not once it is express`() {
        val tomorrow = LocalDate(2026, 9, 11)
        assertTrue(holds(tomorrow, "08:00", "2026-09-10T20:00:00Z", "2026-09-11T02:00:00Z"))
        assertFalse(holds(tomorrow, "08:00", "2026-09-10T20:00:00Z", "2026-09-11T05:00:00Z"))
    }

    @Test
    fun `with no time chosen, or no record of when it was left, only today's rules apply`() {
        assertTrue(holds(null, "", null, "2026-09-10T10:15:00Z"))
        assertTrue(holds(today, "", null, "2026-09-10T10:15:00Z"))
        assertTrue(holds(today, "14:00", null, "2026-09-10T10:15:00Z"))
        assertFalse(holds(today, "12:00", null, "2026-09-10T10:15:00Z"))
    }

    @Test
    fun `the step offers no slot off the day strip or off the quarter-hour grid`() {
        assertNull(slotStateAt(LocalDate(2026, 9, 9), "10:00", now, TimeZone.UTC))
        assertNull(slotStateAt(LocalDate(2026, 9, 18), "10:00", now, TimeZone.UTC))
        assertEquals(SlotState.Available, slotStateAt(LocalDate(2026, 9, 17), "10:00", now, TimeZone.UTC))
        assertNull(slotStateAt(LocalDate(2026, 9, 11), "10:07", now, TimeZone.UTC))
        assertNull(slotStateAt(LocalDate(2026, 9, 11), "20:00", now, TimeZone.UTC))
    }
}
