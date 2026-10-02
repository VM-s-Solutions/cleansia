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

    /** An unlabelled figure over a label guessed for it: a null code is not a CZK order. */
    @Test
    fun `formatOrderPrice renders no unit for a blank currency`() {
        assertEquals("1,200", formatOrderPrice(1200.0, null))
        assertEquals("1,200", formatOrderPrice(1200.0, "  "))
    }

    @Test
    fun `formatOrderPrice passes unknown codes through as a suffix`() {
        assertEquals("1,200 PLN", formatOrderPrice(1200.0, "PLN"))
    }

    /** A credit share can leave haléře; rounding them away shows a figure the card is not charged. */
    @Test
    fun `formatOrderPrice shows the minor units of an amount that is not whole`() {
        assertEquals("319.90 Kč", formatOrderPrice(319.90, "CZK"))
        assertEquals("137.10 Kč", formatOrderPrice(137.10, "CZK"))
        assertEquals("12.50 €", formatOrderPrice(12.5, "EUR"))
        assertEquals("\$1,200.05", formatOrderPrice(1200.05, "USD"))
        assertEquals("-57.60 Kč", formatOrderPrice(-57.6, "CZK"))
    }

    @Test
    fun `formatOrderPrice keeps a whole amount whole, within half a minor unit`() {
        assertEquals("320 Kč", formatOrderPrice(320.0, "CZK"))
        assertEquals("320 Kč", formatOrderPrice(319.999, "CZK"))
        assertEquals("0 Kč", formatOrderPrice(0.0, "CZK"))
        assertEquals("0 Kč", formatOrderPrice(-0.001, "CZK"))
    }

    /** The currency's own minor unit: none for yen; two for a blank or unknown code. */
    @Test
    fun `formatOrderPrice takes the fraction digits from the currency`() {
        assertEquals("1,200 JPY", formatOrderPrice(1199.6, "JPY"))
        assertEquals("319.90", formatOrderPrice(319.9, null))
        assertEquals("319.90 XYZ", formatOrderPrice(319.9, "XYZ"))
    }

    @Test
    fun `formatOrderPrice uses the locale's decimal mark`() {
        assertEquals("319,90 Kč", formatOrderPrice(319.9, "CZK", Locale.forLanguageTag("cs-CZ")))
    }
}
