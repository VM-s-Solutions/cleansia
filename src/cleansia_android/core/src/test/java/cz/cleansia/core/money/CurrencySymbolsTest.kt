package cz.cleansia.core.money

import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Test
import java.util.Locale

/**
 * The defect: on a device whose locale does not know a currency, the JDK hands back the ISO code and
 * the app renders "3 731 CZK" beside a list showing "1 275 Kč".
 *
 * The locale is swapped explicitly here rather than trusting whatever the test JVM defaults to — a
 * suite that passes only because the runner happens to be Czech proves nothing about a Ukrainian phone.
 */
class CurrencySymbolsTest {

    private val original: Locale = Locale.getDefault()

    @After
    fun restore() {
        Locale.setDefault(original)
    }

    /** The reported bug, in the locale that produced it. */
    @Test
    fun `czk renders as a symbol on a ukrainian device`() {
        Locale.setDefault(Locale.forLanguageTag("uk-UA"))
        val symbol = CurrencySymbols.forCode("CZK")
        assertNotEquals("still the bare ISO code", "CZK", symbol)
        assertEquals("Kč", symbol)
    }

    @Test
    fun `czk renders as a symbol in every locale the apps ship`() {
        for (tag in listOf("en-US", "cs-CZ", "sk-SK", "uk-UA", "ru-RU")) {
            Locale.setDefault(Locale.forLanguageTag(tag))
            assertEquals("wrong symbol under $tag", "Kč", CurrencySymbols.forCode("CZK"))
        }
    }

    @Test
    fun `the well known currencies resolve to symbols and not codes`() {
        Locale.setDefault(Locale.forLanguageTag("uk-UA"))
        for (code in listOf("CZK", "EUR", "USD", "PLN")) {
            assertNotEquals("$code fell back to its code", code, CurrencySymbols.forCode(code))
        }
    }

    /**
     * An unknown code is its own best label — it is what the server sent, and inventing something else
     * would be worse than showing it.
     */
    @Test
    fun `an unknown code is returned unchanged`() {
        assertEquals("XYZ", CurrencySymbols.forCode("XYZ"))
        assertEquals("", CurrencySymbols.forCode(null))
        assertEquals("", CurrencySymbols.forCode("   "))
    }

    @Test
    fun `lookup is case insensitive and trimmed`() {
        Locale.setDefault(Locale.forLanguageTag("uk-UA"))
        assertEquals("Kč", CurrencySymbols.forCode(" czk "))
    }
}
