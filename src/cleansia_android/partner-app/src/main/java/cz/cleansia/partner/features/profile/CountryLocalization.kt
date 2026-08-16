package cz.cleansia.partner.features.profile

import cz.cleansia.core.settings.SupportedLanguages
import cz.cleansia.partner.api.model.CountryListItem
import java.util.Locale

/**
 * The country's name in the reader's language.
 *
 * `CountryListItem.name` is the raw `Countries.Name` column, which is English by definition — the seed
 * stores every other language under `Translations`. Reading `.name` directly, which every Android call
 * site did, showed a Czech partner "Czech Republic" in an otherwise fully Czech screen. The web has
 * always resolved it this way (`country.translations?.[currentLang]?.name` in the profile facades); this
 * is the mobile side of the same rule.
 *
 * The lookup goes through [SupportedLanguages.bareCode] because `Locale.getDefault().toLanguageTag()`
 * hands back `"cs-CZ"` on a Czech handset while the translation map is keyed by bare `"cs"` — matching
 * without the narrowing step silently never hits, which is the failure mode this replaces rather than
 * the one it introduces.
 *
 * Falls back through name → ISO code → id, so a country seeded without a translation for this language
 * still renders something a person can read.
 */
fun CountryListItem.localizedName(locale: Locale = Locale.getDefault()): String {
    val code = SupportedLanguages.bareCode(locale.toLanguageTag())
    val translated = code
        ?.let { translations?.get(it) }
        ?.name
        ?.takeIf { it.isNotBlank() }

    return translated
        ?: name?.takeIf { it.isNotBlank() }
        ?: isoCode?.takeIf { it.isNotBlank() }
        ?: id.orEmpty()
}
