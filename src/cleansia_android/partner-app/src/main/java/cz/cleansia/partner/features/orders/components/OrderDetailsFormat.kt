package cz.cleansia.partner.features.orders.components

import cz.cleansia.partner.api.model.OrderAddress
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.util.Locale

/**
 * Shared formatters and slug mappings used across the order-details
 * sub-components. Kept top-level (no Composable wrapping) so they can be
 * used both from previews and from non-UI code (notification builders,
 * etc.) without dragging in a composition context.
 */

internal fun formatOrderDateTime(iso: String?): String? {
    val raw = iso?.takeIf { it.isNotBlank() } ?: return null
    return runCatching {
        val instant = Instant.parse(raw)
        val local = instant.atZone(ZoneId.systemDefault())
        DateTimeFormatter
            .ofLocalizedDateTime(FormatStyle.MEDIUM, FormatStyle.SHORT)
            .withLocale(Locale.getDefault())
            .format(local)
    }.getOrDefault(raw)
}

internal fun formatOrderTime(iso: String?): String? {
    val raw = iso?.takeIf { it.isNotBlank() } ?: return null
    return runCatching {
        val instant = Instant.parse(raw)
        val local = instant.atZone(ZoneId.systemDefault())
        DateTimeFormatter
            .ofLocalizedDateTime(FormatStyle.SHORT, FormatStyle.SHORT)
            .withLocale(Locale.getDefault())
            .format(local)
    }.getOrNull()
}

internal fun formatOrderMoney(amount: Double, currencyCode: String?): String {
    val rounded = String.format(Locale.getDefault(), "%.0f", amount)
    return if (currencyCode.isNullOrBlank()) rounded else "$rounded $currencyCode"
}

internal fun OrderAddress?.formatSingleLine(): String? {
    if (this == null) return null
    val parts = listOfNotNull(
        street?.takeIf { it.isNotBlank() },
        city?.takeIf { it.isNotBlank() },
        zipCode?.takeIf { it.isNotBlank() },
    )
    return parts.joinToString(", ").takeIf { it.isNotBlank() }
}

/**
 * Unicode emoji glyph for a known extra slug — mirrors the customer web
 * wizard's mapping so both surfaces show the same icon for the same
 * extra. Unknown slugs fall back to ✨ so future-seeded extras still
 * render without a code change.
 */
internal fun emojiForExtraSlug(slug: String): String = when (slug) {
    "inside-oven" -> "🔥"
    "inside-fridge" -> "❄️"
    "interior-windows" -> "🪟"
    "laundry-ironing" -> "🧺"
    "pet-hair-supplement" -> "🐾"
    else -> "✨"
}

/**
 * Human-readable name for a known extra slug. Partner-app doesn't fetch
 * the extras catalog (the list cards only show a "+N" count), so we
 * keep names in step with the seed data (`insert_seed_data.sql §7b`).
 * Unknown slugs fall back to the slug with dashes turned into spaces +
 * title-cased — good enough until / unless we add a real catalog fetch.
 */
internal fun nameForExtraSlug(slug: String): String = when (slug) {
    "inside-oven" -> "Inside oven cleaning"
    "inside-fridge" -> "Inside fridge cleaning"
    "interior-windows" -> "Interior windows"
    "laundry-ironing" -> "Laundry & ironing"
    "pet-hair-supplement" -> "Pet hair deep-clean"
    else -> slug.replace('-', ' ').replaceFirstChar { it.uppercase() }
}
