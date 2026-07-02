package cz.cleansia.core.servicearea

import java.util.Locale

/**
 * ISO 3166-1 alpha-3 → alpha-2 normalisation for the service-area seam.
 *
 * The backend stores alpha-3 uppercase ("CZE") while everything Mapbox-facing
 * is alpha-2 lowercase — `GeocodedAddress.countryIsoCode` comes from the
 * feature's `short_code` ("cz") and the forward-geocode `country=` bias param
 * only accepts alpha-2. Comparing the two forms directly can never match, so
 * the data-source adapters normalise to alpha-2 HERE, once, instead of every
 * call site tolerating both.
 *
 * The map is built from the platform's own ISO table ([Locale.getISOCountries])
 * — no hand-maintained list to drift. Unknown or already-alpha-2 inputs pass
 * through lowercased.
 */
object IsoCountryCodes {

    private val alpha3ToAlpha2: Map<String, String> by lazy {
        buildMap {
            for (alpha2 in Locale.getISOCountries()) {
                // isO3Country throws MissingResourceException for codes the
                // platform has no alpha-3 mapping for — skip those.
                runCatching { Locale("", alpha2).isO3Country }
                    .getOrNull()
                    ?.takeIf { it.isNotBlank() }
                    ?.let { put(it.lowercase(), alpha2.lowercase()) }
            }
        }
    }

    /** "CZE"/"cze" → "cz"; "cz" stays "cz"; unknown codes pass through lowercased. */
    fun toAlpha2(code: String?): String {
        val normalized = code?.trim()?.lowercase().orEmpty()
        return alpha3ToAlpha2[normalized] ?: normalized
    }
}
