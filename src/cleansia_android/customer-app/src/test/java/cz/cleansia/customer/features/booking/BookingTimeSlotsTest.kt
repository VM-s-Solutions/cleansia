package cz.cleansia.customer.features.booking

import kotlinx.datetime.Instant
import kotlinx.datetime.LocalDate
import kotlinx.datetime.TimeZone
import org.junit.Assert.assertEquals
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
        assertEquals(SlotState.Earliest, slots["14:15"])
        assertEquals(SlotState.Available, slots["14:30"])
    }

    @Test
    fun `a slot just inside the lead time is unavailable`() {
        val slots = timeSlotsFor(today, Instant.parse("2026-09-10T10:15:01Z"), TimeZone.UTC).associate { it.time to it.state }

        assertEquals(SlotState.Unavailable, slots["12:15"])
        assertEquals(SlotState.Express, slots["12:30"])
    }

    @Test
    fun `selected quarter hour retains its minutes when converted to UTC`() {
        val prague = TimeZone.of("Europe/Prague")

        assertEquals(Instant.parse("2026-09-10T08:15:00Z"), combineDateAndTime(today, "10:15", prague))
        assertEquals(Instant.parse("2026-09-10T08:45:00Z"), combineDateAndTime(today, "10:45", prague))
    }
}
