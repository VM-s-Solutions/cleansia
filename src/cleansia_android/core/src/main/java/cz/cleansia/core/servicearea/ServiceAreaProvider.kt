package cz.cleansia.core.servicearea

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/**
 * Single source of truth for "which countries / cities does the company
 * actually serve". Backs:
 *  - the Mapbox forward-geocode country bias (so suggestions don't
 *    include unserved countries),
 *  - any country pickers shown to users (only serviced countries),
 *  - the inline city service-area indicator on the partner address
 *    section + customer order wizard.
 *
 * Fetched lazily on first access; ONLY a successful server answer is cached
 * (in-memory, for the process lifetime — [refresh] clears it). A FAILED fetch
 * returns null and is NOT cached, so the next access retries: caching the
 * failure used to pin an empty list until force-stop, making the address
 * picker claim "we don't serve this city" for every address after one
 * startup-time network/auth blip.
 *
 * Lives in `:core` so customer-app and partner-app share one
 * implementation; each app supplies its own [ServiceAreaDataSource]
 * adapter that bridges its NSwag-generated API client to the slim
 * core DTOs the provider exposes.
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
