package cz.cleansia.core.servicearea

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/**
 * Single source of truth for which countries and cities the company serves. Backs the geocode bias,
 * country pickers and the service-area indicator.
 *
 * **ONLY a successful answer is cached.** A failed fetch returns null and is not cached, so the next
 * access retries — caching the failure pinned an empty list until force-stop and made the picker claim
 * "we don't serve this city" for every address after one startup blip.
 * -> /mobile-app/patterns#negative-caching
 */
class ServiceAreaProvider(
    private val dataSource: ServiceAreaDataSource,
) {
    private val countriesState = MutableStateFlow<List<ServicedCountry>?>(null)
    private val citiesState = MutableStateFlow<List<ServicedCity>?>(null)
    private val mutex = Mutex()

    val servicedCountries: StateFlow<List<ServicedCountry>?> = countriesState.asStateFlow()
    val servicedCities: StateFlow<List<ServicedCity>?> = citiesState.asStateFlow()

    /**
     * Lazy fetch of countries. Null = the fetch failed and the answer is
     * UNKNOWN — treat it as "couldn't check", never as "serves nothing".
     * Safe to call repeatedly — successes hit the cache, failures retry.
     */
    suspend fun loadCountries(): List<ServicedCountry>? {
        countriesState.value?.let { return it }
        mutex.withLock {
            countriesState.value?.let { return it }
            val fetched = dataSource.fetchServicedCountries() ?: return null
            countriesState.value = fetched
            return fetched
        }
    }

    /**
     * Lazy fetch of cities; null = fetch failed (UNKNOWN). When [countryId]
     * is provided, returns only cities in that country. The unfiltered fetch
     * is cached so subsequent country-scoped calls hit the cache.
     */
    suspend fun loadCities(countryId: String? = null): List<ServicedCity>? {
        citiesState.value?.let { return it.scopedTo(countryId) }
        mutex.withLock {
            citiesState.value?.let { return it.scopedTo(countryId) }
            val fetched = dataSource.fetchServiceCities(countryId = null) ?: return null
            citiesState.value = fetched
            return fetched.scopedTo(countryId)
        }
    }

    /**
     * ISO codes (alpha-2 lowercase) for the Mapbox `country=` bias param.
     * Empty when the list is unavailable — no bias degrades to global
     * suggestions, which is the right failure mode for a UX hint.
     */
    suspend fun servicedCountryIsoCodes(): List<String> =
        loadCountries().orEmpty().map { it.isoCode }

    /**
     * Tri-state: true/false is the server's answer for <cityName> in
     * <countryId> (case + trim-insensitive name match; ZipPrefix unenforced
     * in v1). NULL means the list could not be loaded — callers MUST treat
     * that as unknown and fail OPEN where a server-side re-validation exists
     * (order creation re-checks the city), never render it as "not serviced".
     */
    suspend fun isCityServiced(countryId: String, cityName: String): Boolean? {
        val cities = loadCities(countryId) ?: return null
        val normalized = cityName.trim().lowercase()
        return cities.any { it.name.trim().lowercase() == normalized }
    }

    /** Force re-fetch — call after sign-in or when admin pushes a config change. */
    suspend fun refresh() {
        mutex.withLock {
            countriesState.value = null
            citiesState.value = null
        }
    }

    private fun List<ServicedCity>.scopedTo(countryId: String?): List<ServicedCity> =
        if (countryId == null) this else filter { it.countryId == countryId }
}
