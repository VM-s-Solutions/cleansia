package cz.cleansia.core.servicearea

/**
 * App-side adapter contract — each consumer app (customer, partner)
 * implements this by calling its own NSwag-generated `CountryApi.getServiced()`
 * and `ServiceCityApi.getServiceCities(countryId)` and mapping the
 * results to the `:core` slim DTOs. The shared
 * [ServiceAreaProvider] talks only to this interface, so it doesn't
 * care which app it lives inside.
 *
 * Implementations should:
 *  - return NULL on failure (network error, non-2xx) and log it — never throw.
 *    Null is load-bearing: it tells the provider the answer is UNKNOWN, so the
 *    failure is not cached and callers can fail open. Returning an empty list
 *    on failure is indistinguishable from "the company serves nothing" and
 *    used to poison the address picker for the process lifetime.
 *  - lowercase + alpha-2-normalise ISO codes when filling
 *    [ServicedCountry.isoCode] (see IsoCountryCodes) so Mapbox comparisons and
 *    the country= bias param work without call-site tolerance.
 */
interface ServiceAreaDataSource {
    suspend fun fetchServicedCountries(): List<ServicedCountry>?

    /**
     * [countryId] null = "all serviced cities across countries". When
     * provided, the implementation MAY filter server-side or in-memory
     * — the provider caches the unfiltered list either way.
     */
    suspend fun fetchServiceCities(countryId: String?): List<ServicedCity>?
}
