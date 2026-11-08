package cz.cleansia.partner.features.invoices

import java.util.TimeZone
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

class ShortDateTest {

    /** 22:30 UTC is already the next day in Prague: an invoice keeps the day its timestamp names, a pay line takes the phone's. */
    @Test
    fun `an invoice date is the day its timestamp names, a pay line's the day on the phone`() {
        val original = TimeZone.getDefault()
        try {
            TimeZone.setDefault(TimeZone.getTimeZone("Europe/Prague"))
            assertNotNull(formatShortDate("2026-10-20"))
            assertNotEquals(formatShortDate("2026-10-20"), formatShortDate("2026-10-21"))

            assertEquals(formatShortDate("2026-10-20"), formatShortDate("2026-10-20T22:30:00Z"))
            assertEquals(formatShortDate("2026-10-21"), formatShortDate("2026-10-20T22:30:00Z", inDeviceZone = true))

            assertNull(formatShortDate("2026-10-11T07:15:00"))
            assertEquals(formatShortDate("2026-10-11"), formatShortDate("2026-10-11T07:15:00", inDeviceZone = true))

            assertNull(formatShortDate(" "))
            assertNull(formatShortDate("yesterday", inDeviceZone = true))
        } finally {
            TimeZone.setDefault(original)
        }
    }
}
