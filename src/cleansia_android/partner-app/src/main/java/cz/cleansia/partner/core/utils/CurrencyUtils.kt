package cz.cleansia.partner.core.utils

import java.text.NumberFormat
import java.util.Currency
import java.util.Locale

object CurrencyUtils {

    fun formatCurrency(amount: Double, currency: String = "CZK"): String {
        return try {
            val format = NumberFormat.getCurrencyInstance(Locale.getDefault())
            format.currency = Currency.getInstance(currency)
            format.format(amount)
        } catch (e: Exception) {
            "$currency ${String.format("%.2f", amount)}"
        }
    }

    fun formatCurrencyCompact(amount: Double, currency: String = "CZK"): String {
        return try {
            val format = NumberFormat.getCurrencyInstance(Locale.getDefault())
            format.currency = Currency.getInstance(currency)
            format.maximumFractionDigits = 0
            format.format(amount)
        } catch (e: Exception) {
            "$currency ${String.format("%.0f", amount)}"
        }
    }
}
