package cz.cleansia.customer.features.booking

import kotlinx.datetime.Clock
import kotlinx.datetime.TimeZone
import kotlinx.datetime.toLocalDateTime
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Locale

/**
 * Pins the booking day strip's weekday labels.
 *
 * The strip used to hardcode "Mon".."Sun" in a `when` over DayOfWeek, so a Czech
 * or Ukrainian user picking a date saw English abbreviations next to a fully
 * translated screen. These tests fail against that implementation.
 *
 * The strip always covers today + the next seven days, which is exactly one full
 * week, so the seven non-today chips carry each weekday abbreviation exactly once
 * regardless of what day the suite runs on. That makes the expected label SET a
 * fixed constant per locale — no date mocking needed.
 */
class BookingDayStripTest {

    private val czech = Locale.forLanguageTag("cs")

    @Test
    fun `covers today plus the next seven days`() {
        val days = buildDays(Locale.ENGLISH, "Today")
        val today = Clock.System.now().toLocalDateTime(TimeZone.currentSystemDefault()).date

        assertEquals(8, days.size)
        assertEquals(today, days.first().localDate)
        days.forEachIndexed { index, chip ->
            assertEquals(
                "chip $index should be $index day(s) after today",
                index.toLong(),
                chip.localDate.toEpochDays().toLong() - today.toEpochDays().toLong(),
            )
            assertEquals(chip.localDate.dayOfMonth.toString(), chip.date)
            assertTrue("every day is bookable", chip.available)
            assertEquals("only the first chip is today", index == 0, chip.isToday)
        }
    }

    @Test
    fun `first chip uses the caller-supplied today label`() {
        assertEquals("Today", buildDays(Locale.ENGLISH, "Today").first().label)
        assertEquals("Dnes", buildDays(czech, "Dnes").first().label)
    }

    @Test
    fun `weekday labels are Czech under a Czech locale`() {
        val labels = buildDays(czech, "Dnes").drop(1).map { it.label }

        assertEquals("seven distinct weekdays", 7, labels.toSet().size)
        assertEquals(
            setOf("po", "út", "st", "čt", "pá", "so", "ne"),
            labels.toSet(),
        )
    }

    @Test
    fun `weekday labels stay English under an English locale`() {
        val labels = buildDays(Locale.ENGLISH, "Today").drop(1).map { it.label }

        assertEquals("seven distinct weekdays", 7, labels.toSet().size)
        assertEquals(
            setOf("Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"),
            labels.toSet(),
        )
    }

    @Test
    fun `the locale argument actually drives the labels`() {
        // Guards the specific regression: an implementation that ignores its
        // locale parameter and returns hardcoded English passes every
        // English-only assertion above.
        val ukrainian = buildDays(Locale.forLanguageTag("uk"), "Сьогодні").drop(1).map { it.label }
        val english = buildDays(Locale.ENGLISH, "Today").drop(1).map { it.label }

        assertTrue(
            "Ukrainian labels must not be the English ones: $ukrainian",
            ukrainian.toSet().intersect(english.toSet()).isEmpty(),
        )
    }
}
