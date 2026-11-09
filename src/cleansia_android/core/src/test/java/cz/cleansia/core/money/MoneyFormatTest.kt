package cz.cleansia.core.money

import cz.cleansia.core.format.formatOrderPrice
import java.text.DecimalFormatSymbols
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The partner app drew its money with four copies of one formatter and three variants of it. They are one function
 * now, and each look a screen draws is kept as it was, in every language the app ships: the separators are read from
 * the locale, so the figures are pinned without depending on the JDK's own locale data.
 */
class MoneyFormatTest {

    private val locales = listOf("en-US", "cs-CZ", "sk-SK", "uk-UA", "ru-RU").map(Locale::forLanguageTag)

    private fun Locale.group() = DecimalFormatSymbols.getInstance(this).groupingSeparator

    private fun Locale.mark() = DecimalFormatSymbols.getInstance(this).decimalSeparator

    @Test
    fun `a figure is drawn to the cent in the locale's grouping and mark, its symbol after a space`() {
        locales.forEach { l ->
            assertEquals("$l", "1${l.group()}275${l.mark()}50 Kč", formatMoney(1275.5, "Kč", locale = l))
            assertEquals("$l", "9${l.group()}812${l.mark()}55", formatMoney(9812.55, " ", locale = l))
            assertEquals("$l", "0${l.mark()}13 €", formatMoney(0.125, "€", locale = l))
        }
        assertEquals("1,275.50 Kč", formatMoney(1275.5, "Kč", locale = Locale.US))
        assertEquals("-150.25 Kč", formatMoney(-150.25, "Kč", locale = Locale.US))
    }

    @Test
    fun `a whole-unit figure rounds half up and keeps the locale's grouping`() {
        locales.forEach { l ->
            assertEquals("$l", "1${l.group()}276 Kč", formatMoney(1275.5, "Kč", fractionDigits = 0, locale = l))
            assertEquals("$l", "14${l.group()}250", formatMoney(14_250.4, null, fractionDigits = 0, locale = l))
        }
        assertEquals("1,276 Kč", formatMoney(1275.5, "Kč", fractionDigits = 0, locale = Locale.US))
    }

    @Test
    fun `the board's figure groups with a plain space and prints its cents only when it has some`() {
        locales.forEach { l ->
            assertEquals("$l", "1 412${l.mark()}30 Kč", formatMoney(1412.3, "Kč", fractionDigits = null, locale = l))
            assertEquals("$l", "1 275 Kč", formatMoney(1275.0, " Kč ", fractionDigits = null, locale = l))
        }
    }

    @Test
    fun `a pay that is not whole keeps its minor units, thousands still grouped with a space`() {
        val cs = Locale.forLanguageTag("cs-CZ")
        assertEquals("1 412,30 Kč", formatMoney(1412.3, "Kč", fractionDigits = null, locale = cs))
        assertEquals("1 412.30 Kč", formatMoney(1412.3, "Kč", fractionDigits = null, locale = Locale.US))
        assertEquals("12,05", formatMoney(12.05, null, fractionDigits = null, locale = cs))
        // Under half a haléř from whole is whole, as on the detail.
        assertEquals("1 275 Kč", formatMoney(1274.999, "Kč", fractionDigits = null, locale = cs))
    }

    /**
     * The board and the history rows format a job's pay with the board's rule; its detail formats the same
     * `estimatedCleanerPay` with `formatOrderPrice`. That one prints the haléře of an amount that is not whole, so
     * rounding them on the board showed 412.30 as "412 Kč" there and "412,30 Kč" on the detail. Pay is booked to the
     * haléř, and a seat's share of a job need not be whole.
     */
    @Test
    fun `a job's pay reads the same on the board as on its detail`() {
        val cs = Locale.forLanguageTag("cs-CZ")
        listOf(412.30, 412.5, 0.4, 412.0, 0.0).forEach { pay ->
            listOf(cs, Locale.US).forEach { locale ->
                assertEquals("$pay in $locale", formatOrderPrice(pay, "CZK", locale), formatMoney(pay, "Kč", fractionDigits = null, locale = locale))
                assertEquals("$pay in $locale", formatOrderPrice(pay, "EUR", locale), formatMoney(pay, "€", fractionDigits = null, locale = locale))
            }
        }
    }
}
