package cz.cleansia.partner.features.earnings

import java.text.DecimalFormatSymbols
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Test

class EarningsMoneyTest {

    /** The earnings figures are whole units in each language the app ships, and "no stats" reads as a dash. */
    @Test
    fun `an earnings figure is a whole unit in the phone's grouping, and no stats is a dash`() {
        val original = Locale.getDefault()
        try {
            for (tag in listOf("en-US", "cs-CZ", "sk-SK", "uk-UA", "ru-RU")) {
                val locale = Locale.forLanguageTag(tag)
                Locale.setDefault(locale)
                val group = DecimalFormatSymbols.getInstance(locale).groupingSeparator
                assertEquals(tag, "14${group}251 Kč", formatWholeMoney(14_250.5, "Kč"))
                assertEquals(tag, "781", formatWholeMoney(780.5, ""))
                assertEquals(tag, "—", formatWholeMoney(null, "Kč"))
            }
            Locale.setDefault(Locale.US)
            assertEquals("14,251 Kč", formatWholeMoney(14_250.5, "Kč"))
        } finally {
            Locale.setDefault(original)
        }
    }
}
