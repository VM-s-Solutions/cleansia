package cz.cleansia.partner.features.invoices

import java.util.Locale
import java.util.TimeZone
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
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
}
