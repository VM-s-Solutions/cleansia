package cz.cleansia.partner.core.market

import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
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
    suspend fun getMarkets(): ApiResult<List<MarketListItem>> = safeApiCall(json) { api.getOverview() }
}
