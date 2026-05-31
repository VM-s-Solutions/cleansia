package cz.cleansia.core.location

import kotlinx.serialization.Serializable

/**
 * Result of a forward or reverse Mapbox geocode. The shared shape that
 * the address picker fills as the user moves the map pin / picks a
 * search suggestion; both apps consume this directly when persisting
 * to their respective backend models.
 *
 * `@Serializable` so it can ride a SavedStateHandle (encoded as JSON
 * string) when the picker hands its pick back to the launching screen.
 */
@Serializable
data class GeocodedAddress(
    val latitude: Double,
    val longitude: Double,
    val street: String,
    val city: String,
    val zipCode: String,
    val country: String,
    /**
     * ISO-3166 alpha-2 country code from Mapbox `short_code` (lowercase,
     * e.g. "cz"). Backend stores countries by ID; consumers look up the
     * ID from a cached country list using this code. Empty when Mapbox
     * didn't return a country context (defensive — shouldn't happen for
     * biased forward-geocode results).
     */
    val countryIsoCode: String,
    val formatted: String,
)
