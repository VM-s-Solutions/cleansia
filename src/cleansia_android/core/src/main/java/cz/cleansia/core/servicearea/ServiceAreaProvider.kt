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
 * Fetched lazily on first access, cached in-memory for the process
 * lifetime. Re-fetching only happens when [refresh] is explicitly
 * called (e.g. after sign-in or settings change) — admin-side
 * IsServiced toggles propagate on the next cold start, which is
 * acceptable for a product that expands one country at a time.
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

    /** Lazy fetch of countries. Safe to call repeatedly — subsequent calls hit the cache. */
    suspend fun loadCountries(): List<ServicedCountry> {
        countriesState.value?.let { return it }
        mutex.withLock {
            countriesState.value?.let { return it }
            val fetched = dataSource.fetchServicedCountries()
            countriesState.value = fetched
            return fetched
        }
    }

    /**
     * Lazy fetch of cities. When [countryId] is provided, returns only
     * cities in that country. The unfiltered fetch is cached so
     * subsequent country-scoped calls hit the cache.
     */
    suspend fun loadCities(countryId: String? = null): List<ServicedCity> {
        citiesState.value?.let { cached ->
            return if (countryId == null) cached
            else cached.filter { it.countryId == countryId }
        }
        mutex.withLock {
            citiesState.value?.let { cached ->
                return if (countryId == null) cached
                else cached.filter { it.countryId == countryId }
            }
            val fetched = dataSource.fetchServiceCities(countryId = null)
            citiesState.value = fetched
            return if (countryId == null) fetched
            else fetched.filter { it.countryId == countryId }
        }
    }

    /**
     * ISO codes (lowercase) for the Mapbox `country=` bias param.
     * The provider already normalises in the data-source adapter, so
     * this is a straight projection.
     */
    suspend fun servicedCountryIsoCodes(): List<String> =
        loadCountries().map { it.isoCode }

    /**
     * True iff <cityName> matches a city we serve in <countryId>,
     * case + trim-insensitively. Falls back to city-name-only match
     * (ZipPrefix on the schema isn't enforced in v1).
     */
    suspend fun isCityServiced(countryId: String, cityName: String): Boolean {
        val cities = loadCities(countryId)
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
}
