package cz.cleansia.partner.features.orders

import cz.cleansia.core.money.CurrencySymbols
import cz.cleansia.partner.api.model.OrderListItem
import java.text.DecimalFormatSymbols
import java.util.Locale
import kotlin.math.abs

/**
 * The board and the history list are not scoped to one currency — the backend keeps a EUR job on a
 * CZK cleaner's board on purpose — so their totals used to add koruna to euros and print the sum
 * unlabelled. A sum across two currencies is not a number: the total is one figure per currency,
 * grouped by the ISO code (the symbol is display only), with the currency most rows are in first.
 * Orders whose currency the wire dropped form their own unlabelled group rather than borrowing one.
 */
fun earningsByCurrency(orders: List<OrderListItem>): List<Pair<String?, Double>> =
    orders
        .groupBy { it.currency?.code?.trim()?.takeIf { code -> code.isNotEmpty() }?.uppercase() }
        .entries
        .sortedWith(compareByDescending<Map.Entry<String?, List<OrderListItem>>> { it.value.size }.thenBy(nullsLast()) { it.key })
        .map { (code, group) -> code to group.sumOf { it.estimatedCleanerPay ?: 0.0 } }

/** "1 275 Kč · 40 €" — each currency's own figure, joined; "0" for an empty list. */
fun formatEarningsTotal(orders: List<OrderListItem>): String {
    val groups = earningsByCurrency(orders)
    if (groups.isEmpty()) return formatMoney(0.0, null)
    return groups.joinToString(" · ") { (code, amount) ->
        val symbol = orders
            .firstOrNull { it.currency?.code?.trim()?.uppercase() == code }
            ?.currency?.symbol?.trim()?.takeIf { it.isNotEmpty() }
            ?: CurrencySymbols.forCode(code)
        formatMoney(amount, symbol)
    }
}

/**
 * Thousands grouped with a space, the symbol after; no unit when there is none. A whole amount prints
 * without a fraction and any other to two decimals in the locale's mark: the order detail's rule (core
 * `formatOrderPrice`), so a job's pay reads the same on the board as on its detail. Pay is booked to
 * the haléř, and a seat's share of a job need not be whole: 412.30 is "412,30 Kč" on both, never
 * "412 Kč" here. Only the symbol reaches this function, so the minor unit is two digits: the koruna's
 * and the euro's, and the detail's own fallback for a code it cannot look up.
 */
internal fun formatMoney(amount: Double, currencySymbol: String?, locale: Locale = Locale.getDefault()): String {
    val cents = Math.round(amount * 100)
    val whole = (cents / 100).toString().reversed().chunked(3).joinToString(" ").reversed()
    val minor = abs(cents % 100)
    val number = if (minor == 0L) {
        whole
    } else {
        whole + DecimalFormatSymbols.getInstance(locale).decimalSeparator + minor.toString().padStart(2, '0')
    }
    val sym = currencySymbol?.trim().orEmpty()
    return if (sym.isEmpty()) number else "$number $sym"
}
