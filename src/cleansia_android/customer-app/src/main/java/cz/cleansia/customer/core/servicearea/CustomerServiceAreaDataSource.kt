package cz.cleansia.customer.core.servicearea

import android.util.Log
import cz.cleansia.core.servicearea.IsoCountryCodes
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
 * Failures are logged and surfaced as NULL (never thrown): null tells the
 * provider the answer is UNKNOWN so it does not cache the failure and callers
 * can fail open — an empty list on failure is indistinguishable from "the
 * company serves nothing" and used to pin the address picker on "city not
 * serviced" for the process lifetime.
 */
@Singleton
class CustomerServiceAreaDataSource @Inject constructor(
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
            response.body().orEmpty().mapNotNull { dto ->
                val id = dto.id ?: return@mapNotNull null
                ServicedCountry(
                    id = id,
                    // Normalised to ISO alpha-2 lowercase: the backend stores
                    // alpha-3 ("CZE") but everything Mapbox-facing (short_code
                    // matches, the country= bias param) is alpha-2 ("cz") — a
                    // raw-lowercased alpha-3 can never equality-match either.
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
        }.getOrNull()
}
