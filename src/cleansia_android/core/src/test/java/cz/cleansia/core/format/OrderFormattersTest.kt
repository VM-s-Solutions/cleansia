package cz.cleansia.core.format

import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import java.util.Locale
import java.util.TimeZone

/**
 * Pins the one canonical rendering of order date / time / money that both the
 * customer and partner apps now share. Inputs are UTC ISO-8601; the device
 * timezone is forced to UTC so the time-of-day component is deterministic.
 */
class OrderFormattersTest {

    private lateinit var savedLocale: Locale
    private lateinit var savedZone: TimeZone

    @Before
    fun fixEnvironment() {
        savedLocale = Locale.getDefault()
        savedZone = TimeZone.getDefault()
        Locale.setDefault(Locale.ENGLISH)
        TimeZone.setDefault(TimeZone.getTimeZone("UTC"))
    }

    @After
    fun restoreEnvironment() {
        Locale.setDefault(savedLocale)
        TimeZone.setDefault(savedZone)
    }

    @Test
    fun `formatOrderDateTime renders month-day and 24h time`() {
        assertEquals("Apr 22 · 10:00", formatOrderDateTime("2026-04-22T10:00:00Z"))
    }

    @Test
    fun `formatOrderDateTime returns dash for null or blank`() {
        assertEquals("—", formatOrderDateTime(null))
        assertEquals("—", formatOrderDateTime("   "))
    }

    @Test
    fun `formatOrderDateTime echoes raw input when unparseable`() {
        assertEquals("not-a-date", formatOrderDateTime("not-a-date"))
    }

    @Test
    fun `formatOrderTime renders 24h time only`() {
        assertEquals("10:00", formatOrderTime("2026-04-22T10:00:00Z"))
    }

    @Test
    fun `formatOrderTime returns dash for null or blank`() {
        assertEquals("—", formatOrderTime(null))
        assertEquals("—", formatOrderTime(""))
    }

    @Test
    fun `formatOrderPrice maps known currency codes to native symbols`() {
        assertEquals("1,200 Kč", formatOrderPrice(1200.0, "CZK"))
        assertEquals("1,200 €", formatOrderPrice(1200.0, "EUR"))
        assertEquals("\$1,200", formatOrderPrice(1200.0, "USD"))
    }

    @Test
    fun `formatOrderPrice defaults blank currency to CZK`() {
        assertEquals("1,200 Kč", formatOrderPrice(1200.0, null))
    }

    @Test
    fun `formatOrderPrice passes unknown codes through as a suffix`() {
        assertEquals("1,200 PLN", formatOrderPrice(1200.0, "PLN"))
    }
}
