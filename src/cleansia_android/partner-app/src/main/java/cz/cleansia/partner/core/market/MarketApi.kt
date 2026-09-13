package cz.cleansia.partner.core.market

import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.GET

/**
 * Hand-written Retrofit interface for the anonymous market directory. Hand-written rather than
 * generated because the checked-in partner spec is refreshed from a running host
 * (`:partner-app:dumpOpenApiSpec`) and does not carry `Market/GetOverview` yet — the same precedent
 * as [cz.cleansia.partner.core.devices.DeviceManagementApi]. Replace with the generated
 * `cz.cleansia.partner.api.client.MarketApi` once the spec carries it.
 */
interface MarketApi {

    @GET("api/Market/GetOverview")
    suspend fun getOverview(): Response<List<MarketListItem>>
}

/**
 * The customer spec's `MarketListItem`, restricted to what the register picker renders and sends:
 * [countryId] is what `RegisterEmployee` carries, [isDefault] decides the pre-selection, the rest
 * is the label. The currency and copy figures are the customer app's business and are ignored here.
 * -> /decisions/adr-0058
 */
@Serializable
data class MarketListItem(
    val countryId: String,
    val isoCode: String,
    val isoAlpha2: String,
    val name: String,
    val translations: Map<String, MarketTranslation>? = null,
    val isDefault: Boolean,
)

@Serializable
data class MarketTranslation(
    val name: String? = null,
    val description: String? = null,
)

/** ADR-0058 D2: the flagged default, else the first listed; null only for an empty directory. */
fun List<MarketListItem>.defaultOrFirst(): MarketListItem? = firstOrNull { it.isDefault } ?: firstOrNull()
