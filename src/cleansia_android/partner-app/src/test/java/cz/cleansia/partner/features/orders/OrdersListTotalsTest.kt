package cz.cleansia.partner.features.orders

import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.partner.api.model.CurrencyListItem
import cz.cleansia.partner.api.model.OrderListItem
import java.text.DecimalFormatSymbols
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The list totals used to sum `estimatedCleanerPay` across every order on the page and label the sum
 * with the one symbol all orders shared — or with nothing when they did not. A CZ cleaner with one
 * EUR job on the board read "1 315": koruna plus euros, unlabelled. The backend keeps mixed lists by
 * design, so the client answers one figure per currency.
 */
class OrdersListTotalsTest {

    private val czk = CurrencyListItem(id = "cur-czk", code = "CZK", symbol = "Kč", name = "Czech koruna", isDefault = true)
    private val eur = CurrencyListItem(id = "cur-eur", code = "EUR", symbol = "€", name = "Euro", isDefault = false)

    private fun order(pay: Double?, currency: CurrencyListItem?) =
        OrderListItem(id = "o-${pay}-${currency?.code}", estimatedCleanerPay = pay, currency = currency)

    @Test
    fun `a single-currency list is one labelled figure`() {
        val orders = listOf(order(1000.0, czk), order(275.0, czk))

        assertEquals(listOf("CZK" to 1275.0), earningsByCurrency(orders))
        assertEquals("1 275 Kč", formatEarningsTotal(orders))
    }

    @Test
    fun `a mixed list is one figure per currency, never a sum across them`() {
        val orders = listOf(order(1000.0, czk), order(40.0, eur), order(275.0, czk))

        assertEquals(listOf("CZK" to 1275.0, "EUR" to 40.0), earningsByCurrency(orders))
        assertEquals("1 275 Kč · 40 €", formatEarningsTotal(orders))
    }

    /** Grouping is by ISO code, not by the display symbol the server happens to send. */
    @Test
    fun `orders are grouped by code even when their symbols differ`() {
        val czkSpelledOut = czk.copy(symbol = "CZK")
        val orders = listOf(order(1000.0, czk), order(275.0, czkSpelledOut))

        assertEquals(listOf("CZK" to 1275.0), earningsByCurrency(orders))
        assertEquals("1 275 Kč", formatEarningsTotal(orders))
    }

    @Test
    fun `orders whose currency the wire dropped form their own unlabelled group`() {
        val orders = listOf(order(1000.0, czk), order(50.0, null), order(275.0, czk))

        assertEquals(listOf("CZK" to 1275.0, null to 50.0), earningsByCurrency(orders))
        assertEquals("1 275 Kč · 50", formatEarningsTotal(orders))
    }

    @Test
    fun `an absent pay counts as nothing rather than dropping the order's currency`() {
        val orders = listOf(order(null, eur), order(40.0, eur))

        assertEquals(listOf("EUR" to 40.0), earningsByCurrency(orders))
    }

    /**
     * The board and the history rows format a job's pay with [formatMoney]; its detail formats the same
     * `estimatedCleanerPay` with core's `formatOrderPrice`. That one prints the haléře of an amount that
     * is not whole, so rounding them here showed 412.30 as "412 Kč" on the board and "412,30 Kč" on the
     * detail. Pay is booked to the haléř, and a seat's share of a job need not be whole.
     */
    @Test
    fun `a job's pay reads the same on the board as on its detail`() {
        val cs = Locale.forLanguageTag("cs-CZ")
        listOf(412.30, 412.5, 0.4, 412.0, 0.0).forEach { pay ->
            listOf(cs, Locale.US).forEach { locale ->
                assertEquals("$pay in $locale", formatOrderPrice(pay, "CZK", locale), formatMoney(pay, "Kč", locale))
                assertEquals("$pay in $locale", formatOrderPrice(pay, "EUR", locale), formatMoney(pay, "€", locale))
            }
        }
    }

    @Test
    fun `a pay that is not whole keeps its minor units, thousands still grouped with a space`() {
        val cs = Locale.forLanguageTag("cs-CZ")
        assertEquals("1 412,30 Kč", formatMoney(1412.3, "Kč", cs))
        assertEquals("1 412.30 Kč", formatMoney(1412.3, "Kč", Locale.US))
        assertEquals("12,05", formatMoney(12.05, null, cs))
        // Under half a haléř from whole is whole, as on the detail.
        assertEquals("1 275 Kč", formatMoney(1274.999, "Kč", cs))
    }

    /** A total of pays that are not whole is not rounded either. */
    @Test
    fun `the list total keeps the minor units of its rows`() {
        val orders = listOf(order(412.30, czk), order(500.0, czk))

        assertEquals("912${DecimalFormatSymbols.getInstance().decimalSeparator}30 Kč", formatEarningsTotal(orders))
    }

    @Test
    fun `an empty list is zero`() {
        assertEquals(emptyList<Pair<String?, Double>>(), earningsByCurrency(emptyList()))
        assertEquals("0", formatEarningsTotal(emptyList()))
    }
}
