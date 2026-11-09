package cz.cleansia.partner.features.invoices

import java.time.LocalDate
import java.util.Locale
import java.util.TimeZone
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ShortDateTest {

    /** 22:30 UTC is already the next day in Prague: an invoice and a pay line paid then both show the phone's day. */
    @Test
    fun `every date is the day on the phone, so an invoice and its pay line agree`() {
        val original = TimeZone.getDefault()
        try {
            TimeZone.setDefault(TimeZone.getTimeZone("Europe/Prague"))
            assertNotNull(formatShortDate("2026-10-20"))
            assertNotEquals(formatShortDate("2026-10-20"), formatShortDate("2026-10-21"))

            assertEquals(formatShortDate("2026-10-21"), formatShortDate("2026-10-20T22:30:00Z"))
            assertEquals(formatShortDate("2026-10-11"), formatShortDate("2026-10-11T07:15:00"))

            assertNull(formatShortDate(" "))
            assertNull(formatShortDate("yesterday"))
        } finally {
            TimeZone.setDefault(original)
        }
    }

    /** A language picked in Settings keeps the process, so a date drawn after it takes its month names. */
    @Test
    fun `a date takes the month names of the language set when it is drawn`() {
        val original = Locale.getDefault()
        try {
            Locale.setDefault(Locale.forLanguageTag("cs"))
            val czech = formatShortDate("2026-11-12")
            Locale.setDefault(Locale.ENGLISH)
            assertNotEquals(czech, formatShortDate("2026-11-12"))
        } finally {
            Locale.setDefault(original)
        }
    }

    /** The screens hand in the language their resources resolve, which is the in-app one even where the default lags. */
    @Test
    fun `a date takes the month names of the language it is given`() {
        val original = Locale.getDefault()
        try {
            Locale.setDefault(Locale.ENGLISH)
            val czech = formatShortDate("2026-11-16", Locale.forLanguageTag("cs"))!!
            assertFalse(czech, czech.contains("Nov"))
            assertTrue(czech, czech.startsWith("16 ") && czech.endsWith(" 2026"))
            assertEquals("16 Nov 2026", formatShortDate("2026-11-16", Locale.ENGLISH))
        } finally {
            Locale.setDefault(original)
        }
    }

    @Test
    fun `a pay period reads from its first and last day, each with its year, in the language it is given`() {
        assertEquals("3 Nov 2026 – 16 Nov 2026", formatPeriod("2026-11-03", "2026-11-16", Locale.ENGLISH))
        assertEquals("29 Dec 2026 – 11 Jan 2027", formatPeriod("2026-12-29", "2027-01-11", Locale.ENGLISH))

        val czech = formatPeriod("2026-11-03", "2026-11-16", Locale.forLanguageTag("cs"))!!
        assertFalse(czech, czech.contains("Nov"))
        assertTrue(czech, czech.startsWith("3 ") && czech.contains(" 2026 – 16 ") && czech.endsWith(" 2026"))

        assertNull(formatPeriod(null, "2026-11-16", Locale.ENGLISH))
        assertNull(formatPeriod("2026-11-03", " ", Locale.ENGLISH))
    }

    /** The server names the period's days; read as instants they slid a day, and a date-only value hid the dashboard card. */
    @Test
    fun `a period's day is the one the server names, on a phone in any time zone`() {
        val original = TimeZone.getDefault()
        try {
            for (zone in listOf("America/Los_Angeles", "Europe/Prague", "Pacific/Kiritimati")) {
                TimeZone.setDefault(TimeZone.getTimeZone(zone))
                assertEquals(zone, LocalDate.of(2026, 11, 16), calendarDate("2026-11-16"))
                assertEquals(zone, "16 Nov 2026", formatShortDate("2026-11-16", Locale.ENGLISH))
                assertEquals(zone, "3 Nov 2026 – 16 Nov 2026", formatPeriod("2026-11-03", "2026-11-16", Locale.ENGLISH))
            }
        } finally {
            TimeZone.setDefault(original)
        }
        assertNull(calendarDate(null))
        assertNull(calendarDate("16. 11. 2026"))
    }
}
