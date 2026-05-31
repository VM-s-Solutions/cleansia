package cz.cleansia.partner.core.servicearea

import android.util.Log
import cz.cleansia.core.servicearea.ServiceAreaDataSource
import cz.cleansia.core.servicearea.ServicedCity
import cz.cleansia.core.servicearea.ServicedCountry
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.client.ServiceCityApi
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Bridges the partner-app's NSwag-generated `CountryApi` +
 * `ServiceCityApi` to the `:core` slim DTOs the shared
 * [cz.cleansia.core.servicearea.ServiceAreaProvider] consumes.
 *
 * Mirrors `CustomerServiceAreaDataSource` exactly — only the imported
 * NSwag types differ (partner's vs customer's generated package).
 * Failures are logged + swallowed; the address section's service-area
 * indicator degrades to "unknown" instead of breaking the screen.
 */
@Singleton
class PartnerServiceAreaDataSource @Inject constructor(
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
