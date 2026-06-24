package cz.cleansia.core.format

import kotlinx.datetime.Instant
import kotlinx.datetime.TimeZone
import kotlinx.datetime.toJavaLocalDateTime
import kotlinx.datetime.toLocalDateTime
import java.text.NumberFormat
import java.time.format.DateTimeFormatter
import java.util.Locale
import kotlin.time.Duration.Companion.minutes

/**
 * Pure-Kotlin formatting helpers for Order DTOs. Reused across the Orders tab,
 * Order detail screen and the Home tab's upcoming-order pill. Each helper is
 * defensive — malformed inputs never crash, they return a sensible fallback.
 *
 * Compose-typed helpers (anything returning a `Color` or annotated `@Composable`)
 * live in [cz.cleansia.customer.ui.format] so this package stays framework-pure
 * and safe to depend on from ViewModels.
 */

private fun shortDateTimeFormatter(locale: Locale): DateTimeFormatter =
    // "Apr 22 · 10:00" — month-abbrev day · 24h time. Android-14 devices
    // honour the system's 12/24h preference via DateFormat.is24HourFormat elsewhere;
    // the Orders list stays locale-stable with 24h to match the web app.
    DateTimeFormatter.ofPattern("MMM d · HH:mm", locale)

private fun dateFormatter(locale: Locale): DateTimeFormatter =
    DateTimeFormatter.ofPattern("MMM d", locale)

private fun timeFormatter(locale: Locale): DateTimeFormatter =
    DateTimeFormatter.ofPattern("HH:mm", locale)

/**
 * Parse an ISO-8601 UTC instant and format it in the device timezone.
 * Returns the raw input (or "—" if null) if parsing fails.
 */
fun formatOrderDateTime(iso: String?, locale: Locale = Locale.getDefault()): String {
    if (iso.isNullOrBlank()) return "—"
    return try {
        val instant = Instant.parse(iso)
        val local = instant.toLocalDateTime(TimeZone.currentSystemDefault()).toJavaLocalDateTime()
        local.format(shortDateTimeFormatter(locale))
    } catch (_: Throwable) {
        iso
    }
}

/**
 * Parse an ISO-8601 UTC instant and format just its time-of-day ("10:00")
 * in the device timezone. Returns "—" if null/blank, or the raw input if
 * parsing fails.
 */
fun formatOrderTime(iso: String?, locale: Locale = Locale.getDefault()): String {
    if (iso.isNullOrBlank()) return "—"
    return try {
        val instant = Instant.parse(iso)
        val local = instant.toLocalDateTime(TimeZone.currentSystemDefault()).toJavaLocalDateTime()
        local.format(timeFormatter(locale))
    } catch (_: Throwable) {
        iso
    }
}

/**
 * Format an arrival window: "Apr 22 · 10:00–12:00".
 * Falls back to [formatOrderDateTime] if the end time can't be derived.
 */
fun formatOrderDateRange(
    iso: String?,
    estimatedMinutes: Int,
    locale: Locale = Locale.getDefault(),
): String {
    if (iso.isNullOrBlank()) return "—"
    return try {
        val start = Instant.parse(iso)
        val startLocal = start.toLocalDateTime(TimeZone.currentSystemDefault()).toJavaLocalDateTime()
        if (estimatedMinutes <= 0) {
            return startLocal.format(shortDateTimeFormatter(locale))
        }
        val endLocal = (start + estimatedMinutes.minutes)
            .toLocalDateTime(TimeZone.currentSystemDefault())
            .toJavaLocalDateTime()
        val datePart = startLocal.format(dateFormatter(locale))
        val startTime = startLocal.format(timeFormatter(locale))
        val endTime = endLocal.format(timeFormatter(locale))
        "$datePart · $startTime–$endTime"
    } catch (_: Throwable) {
        formatOrderDateTime(iso, locale)
    }
}

/**
 * Format a price with a currency suffix. Uses the device locale for grouping
 * and decimal marks. Known currencies get their native symbol (Kč, €, $);
 * unknown codes fall through as "123 USD".
 */
fun formatOrderPrice(
    amount: Double,
    currencyCode: String?,
    locale: Locale = Locale.getDefault(),
): String {
    val code = currencyCode?.takeIf { it.isNotBlank() } ?: "CZK"
    val nf = NumberFormat.getNumberInstance(locale).apply {
        maximumFractionDigits = 0
        minimumFractionDigits = 0
    }
    val formatted = nf.format(amount)
    return when (code.uppercase()) {
        "CZK" -> "$formatted Kč"
        "EUR" -> "$formatted €"
        "USD" -> "\$$formatted"
        "GBP" -> "£$formatted"
        else -> "$formatted $code"
    }
}
