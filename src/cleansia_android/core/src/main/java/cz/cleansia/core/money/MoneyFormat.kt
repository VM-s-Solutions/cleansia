package cz.cleansia.core.money

import java.text.DecimalFormatSymbols
import java.util.Locale
import kotlin.math.abs

/**
 * A money figure with its currency symbol after it, "1,275.50 Kč", or the bare number when there is no symbol.
 *
 * A [fractionDigits] count is drawn the way [locale] writes a number: its thousands separator and its decimal mark,
 * rounded half up. Null is the orders board's rule, which is the order detail's
 * ([cz.cleansia.core.format.formatOrderPrice]) with the thousands grouped by a plain space: a whole amount prints
 * without a fraction and any other to two decimals in [locale]'s mark, so a job's pay reads the same on the board as
 * on its detail. Pay is booked to the haléř, and a seat's share of a job need not be whole: 412.30 is "412,30 Kč" on
 * both, never "412 Kč". The board sees only the symbol, so its minor unit is two digits: the koruna's and the euro's,
 * and the detail's own fallback for a code it cannot look up.
 */
fun formatMoney(
    amount: Double,
    symbol: String?,
    fractionDigits: Int? = 2,
    locale: Locale = Locale.getDefault(),
): String {
    val number = if (fractionDigits != null) {
        String.format(locale, "%,.${fractionDigits}f", amount)
    } else {
        val cents = Math.round(amount * 100)
        val whole = (cents / 100).toString().reversed().chunked(3).joinToString(" ").reversed()
        val minor = abs(cents % 100)
        if (minor == 0L) {
            whole
        } else {
            whole + DecimalFormatSymbols.getInstance(locale).decimalSeparator + minor.toString().padStart(2, '0')
        }
    }
    val unit = symbol?.trim().orEmpty()
    return if (unit.isEmpty()) number else "$number $unit"
}
