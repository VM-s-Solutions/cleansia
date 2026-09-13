package cz.cleansia.partner.core.market

import cz.cleansia.core.settings.SupportedLanguages
import java.util.Locale

/**
 * The market's country name in the reader's language — the same rule as
 * [cz.cleansia.partner.features.profile.localizedName] on `CountryListItem`: `name` is the English
 * `Countries.Name` column, the other languages live under `translations` keyed by bare code.
 */
fun MarketListItem.localizedName(locale: Locale = Locale.getDefault()): String {
    val code = SupportedLanguages.bareCode(locale.toLanguageTag())
    return code
        ?.let { translations?.get(it) }
        ?.name
        ?.takeIf { it.isNotBlank() }
        ?: name.takeIf { it.isNotBlank() }
        ?: isoCode
}
