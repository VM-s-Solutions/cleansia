package cz.cleansia.partner.core.servicearea

import android.util.Log
import cz.cleansia.core.servicearea.IsoCountryCodes
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

    override suspend fun fetchServicedCountries(): List<ServicedCountry>? =
        runCatching<List<ServicedCountry>?> {
            val response = countryApi.countryGetServiced()
            if (!response.isSuccessful) {
                Log.w("ServiceArea", "countryGetServiced failed: HTTP ${response.code()}")
                return@runCatching null
            }
            // Never `orEmpty()`: this function's null IS the "couldn't check" answer
            // (`ServiceAreaProvider.loadCountries` — *"treat it as 'couldn't check', never as
            // 'serves nothing'"*), so defaulting a bodiless 2xx to an empty list states the one
            // reading the caller's own contract forbids.
            (response.body() ?: return@runCatching null).mapNotNull { dto ->
                val id = dto.id ?: return@mapNotNull null
                ServicedCountry(
                    id = id,
                    // Normalised to ISO alpha-2 lowercase — see IsoCountryCodes
                    // (backend stores alpha-3; Mapbox-facing code is alpha-2).
                    isoCode = IsoCountryCodes.toAlpha2(dto.isoCode),
                    name = dto.name.orEmpty(),
                )
            }
        }.onFailure {
            Log.w("ServiceArea", "Failed to load serviced countries", it)
        }.getOrNull()

    override suspend fun fetchServiceCities(countryId: String?): List<ServicedCity>? =
        runCatching<List<ServicedCity>?> {
            val response = serviceCityApi.serviceCityGetServiceCities(countryId = countryId)
            if (!response.isSuccessful) {
                Log.w("ServiceArea", "serviceCityGetServiceCities failed: HTTP ${response.code()}")
                return@runCatching null
            }
            (response.body() ?: return@runCatching null).mapNotNull { dto ->
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
        }.getOrNull()
}
