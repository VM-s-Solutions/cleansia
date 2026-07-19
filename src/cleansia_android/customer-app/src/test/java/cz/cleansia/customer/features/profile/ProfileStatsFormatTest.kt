package cz.cleansia.customer.features.profile

import java.util.Locale
import kotlinx.datetime.Instant
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ProfileStatsFormatTest {

    private val originalLocale: Locale = Locale.getDefault()

    @After
    fun tearDown() {
        Locale.setDefault(originalLocale)
    }

    @Test
    fun `formatSaved maps known currency codes to symbols`() {
        Locale.setDefault(Locale.ENGLISH)
        assertEquals("320 Kč", formatSaved(320.0, "CZK"))
        assertEquals("45 €", formatSaved(45.0, "EUR"))
        assertEquals("10 $", formatSaved(10.0, "USD"))
    }

    @Test
    fun `formatSaved passes an unknown currency code through`() {
        Locale.setDefault(Locale.ENGLISH)
        assertEquals("5 PLN", formatSaved(5.0, "PLN"))
    }

    @Test
    fun `formatSaved omits the symbol when the user has no realized currency`() {
        Locale.setDefault(Locale.ENGLISH)
        assertEquals("0", formatSaved(0.0, null))
    }

    @Test
    fun `formatMemberSince renders abbreviated month plus year in English`() {
        Locale.setDefault(Locale.ENGLISH)
        assertEquals("Feb 2025", formatMemberSince(Instant.parse("2025-02-14T12:00:00Z")))
    }

    @Test
    fun `formatMemberSince localizes the month for non-English locales`() {
        Locale.setDefault(Locale.forLanguageTag("cs"))
        val formatted = formatMemberSince(Instant.parse("2025-02-14T12:00:00Z"))
        assertTrue(formatted.contains("2025"))
        assertNotEquals("Feb 2025", formatted)
    }

    @Test
    fun `formatMemberSince falls back to an em dash when unknown`() {
        assertEquals("—", formatMemberSince(null))
    }
}
