package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.CurrencyListItem
import cz.cleansia.partner.api.model.OrderListItem
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

    @Test
    fun `an empty list is zero`() {
        assertEquals(emptyList<Pair<String?, Double>>(), earningsByCurrency(emptyList()))
        assertEquals("0", formatEarningsTotal(emptyList()))
    }
}
