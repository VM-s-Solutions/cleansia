package cz.cleansia.partner.features.orders

import cz.cleansia.core.money.CurrencySymbols
import cz.cleansia.partner.api.model.OrderListItem
import kotlin.math.roundToInt

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

/** Whole units, thousands grouped with a space, the symbol after; no unit when there is none. */
internal fun formatMoney(amount: Double, currencySymbol: String?): String {
    val rounded = amount.roundToInt()
    val whole = rounded.toString().reversed().chunked(3).joinToString(" ").reversed()
    val sym = currencySymbol?.trim().orEmpty()
    return if (sym.isEmpty()) whole else "$whole $sym"
}
