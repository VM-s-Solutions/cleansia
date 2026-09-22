package cz.cleansia.partner.core.market

import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.partner.api.client.MarketApi
import cz.cleansia.partner.api.model.MarketListItem
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

/**
 * The markets a cleaner may register in — a serviced country with a configured currency AND an
 * operating company (ADR-0061 D2). Read from `Market/GetOverview` rather than `Country/GetServiced`
 * because the latter still lists a country nobody operates, which `RegisterEmployee` would refuse
 * with `tenant.not_found`.
 */
@Singleton
class MarketRepository @Inject constructor(
    private val api: MarketApi,
    private val json: Json,
) {
    suspend fun getMarkets(): ApiResult<List<Market>> =
        safeApiCall(json) { api.marketGetOverview() }.mapWire { rows -> rows.map { it.toDomain() } }
}

/**
 * A row with no id cannot be sent and one with no default flag cannot be preselected, so either
 * refuses the whole read rather than inventing a market a cleaner could register into.
 */
internal fun MarketListItem.toDomain() = Market(
    countryId = countryId.required("countryId"),
    isoCode = isoCode.orEmpty(),
    name = name.orEmpty(),
    translatedNames = translations.orEmpty()
        .mapNotNull { (code, translation) -> translation.name?.takeIf { it.isNotBlank() }?.let { code to it } }
        .toMap(),
    isDefault = isDefault.required("isDefault"),
)
