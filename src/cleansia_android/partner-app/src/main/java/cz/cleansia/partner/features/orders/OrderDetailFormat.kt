package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderAddress

/**
 * Address and extra-slug helpers used across the order-details sub-components.
 * Order date/time/money formatting is shared with the customer app via
 * [cz.cleansia.core.format].
 */

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
