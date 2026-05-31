package cz.cleansia.customer.core.servicearea

import android.util.Log
import cz.cleansia.core.servicearea.ServiceAreaDataSource
import cz.cleansia.core.servicearea.ServicedCity
import cz.cleansia.core.servicearea.ServicedCountry
import cz.cleansia.customer.api.client.CountryApi
import cz.cleansia.customer.api.client.ServiceCityApi
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Bridges the customer-app's NSwag-generated `CountryApi` +
 * `ServiceCityApi` to the `:core` slim DTOs the shared
 * [cz.cleansia.core.servicearea.ServiceAreaProvider] consumes.
 *
 * Failures are logged + swallowed: a transient network blip shouldn't
 * promote a hard error all the way to the UI, since the address picker
 * + Mapbox bias degrade gracefully (no bias = global suggestions).
 */
@Singleton
class CustomerServiceAreaDataSource @Inject constructor(
    private val countryApi: CountryApi,
    private val serviceCityApi: ServiceCityApi,
) : ServiceAreaDataSource {

    override suspend fun fetchServicedCountries(): List<ServicedCountry> =
        runCatching {
            val response = countryApi.countryGetServiced()
            if (!response.isSuccessful) return@runCatching emptyList()
            response.body().orEmpty().mapNotNull { dto ->
                val id = dto.id ?: return@mapNotNull null
                ServicedCountry(
                    id = id,
                    // Mapbox bias expects ISO-3166 alpha-2 lowercase.
                    // Backend stores alpha-3 uppercase (e.g. "CZE");
                    // converting alpha-3 → alpha-2 here would require a
                    // lookup table — for now we lowercase the alpha-3
                    // and let downstream callers tolerate either form.
                    // See servicedCountryIsoCodes() comment in the
                    // provider for the trade-off.
                    isoCode = dto.isoCode?.lowercase().orEmpty(),
                    name = dto.name.orEmpty(),
                )
            }
        }.onFailure {
            Log.w("ServiceArea", "Failed to load serviced countries", it)
        }.getOrDefault(emptyList())

    override suspend fun fetchServiceCities(countryId: String?): List<ServicedCity> =
        runCatching {
            val response = serviceCityApi.serviceCityGetServiceCities(countryId = countryId)
            if (!response.isSuccessful) return@runCatching emptyList()
            response.body().orEmpty().mapNotNull { dto ->
                val id = dto.id ?: return@mapNotNull null
                val cityCountryId = dto.countryId ?: return@mapNotNull null
                ServicedCity(
                    id = id,
                    countryId = cityCountryId,
                    name = dto.name.orEmpty(),
                )
            }
        }.onFailure {
            Log.w("ServiceArea", "Failed to load service cities", it)
        }.getOrDefault(emptyList())
}
