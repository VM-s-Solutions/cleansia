package cz.cleansia.core.money

import java.util.Currency
import java.util.Locale

/**
 * The display symbol for an ISO currency code — "CZK" → "Kč", "EUR" → "€".
 *
 * **Why this is not just `Currency.getSymbol()`.** That overload formats for the DEVICE locale, and the
 * JDK returns the bare ISO code whenever it has no symbol for that currency in that locale. On a
 * Ukrainian handset `Currency.getInstance("CZK").getSymbol(Locale.getDefault())` is literally `"CZK"`,
 * so the partner dashboard read "3 731 CZK" while the orders list beside it read "1 275 Kč" — the list
 * uses the symbol the server sends on each order, the dashboard only receives a code.
 *
 * A currency's symbol is a property of the CURRENCY, not of who is looking at it: a Czech cleaner sees
 * "Kč" whatever language their phone is in. So the symbol is resolved in a locale that actually uses
 * the currency, discovered from the JDK's own locale data rather than from a hand-kept table that would
 * go stale the first time a market opens.
 */
object CurrencySymbols {

    private val cache = HashMap<String, String>()

    /**
     * Never returns blank: an unknown code is its own best label, which is what the server would have
     * sent anyway.
     */
    fun forCode(isoCode: String?): String {
        val code = isoCode?.trim()?.takeIf { it.isNotEmpty() } ?: return ""
        return cache.getOrPut(code.uppercase()) { resolve(code.uppercase()) }
    }

    private fun resolve(code: String): String {
        val currency = runCatching { Currency.getInstance(code) }.getOrNull() ?: return code

        // The device locale first — it is the right answer whenever the reader's locale knows the
        // currency, and it keeps a Czech phone on "Kč" without any scanning at all.
        val deviceSymbol = runCatching { currency.getSymbol(Locale.getDefault()) }.getOrNull()
        if (deviceSymbol != null && deviceSymbol != code) return deviceSymbol

        // The device locale had nothing, so ask a locale that actually spends this currency.
        val nativeSymbol = Locale.getAvailableLocales()
            .asSequence()
            .filter { it.country.isNotEmpty() }
            .filter { runCatching { Currency.getInstance(it) }.getOrNull() == currency }
            .mapNotNull { runCatching { currency.getSymbol(it) }.getOrNull() }
            .firstOrNull { it != code }

        return nativeSymbol ?: code
    }
}
