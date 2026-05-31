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
 *  - return an empty list on failure (not throw — the provider treats
 *    the lookup as best-effort and the UI degrades gracefully),
 *  - lowercase ISO codes when filling [ServicedCountry.isoCode] so the
 *    Mapbox bias param doesn't need to re-normalize.
 */
interface ServiceAreaDataSource {
    suspend fun fetchServicedCountries(): List<ServicedCountry>

    /**
     * [countryId] null = "all serviced cities across countries". When
     * provided, the implementation MAY filter server-side or in-memory
     * — the provider caches the unfiltered list either way.
     */
    suspend fun fetchServiceCities(countryId: String?): List<ServicedCity>
}
