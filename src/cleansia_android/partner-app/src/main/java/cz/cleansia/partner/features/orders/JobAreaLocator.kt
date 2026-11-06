package cz.cleansia.partner.features.orders

import cz.cleansia.core.location.ReverseGeocodingService
import java.util.concurrent.ConcurrentHashMap
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Where the map behind a job she has not taken is centred: the area the board already shows ("Praha 8", "Brno"),
 * geocoded within its market's country, else the market's main city. It never asks for the job's own coordinates —
 * the server sends none before the take.
 */
@Singleton
class JobAreaLocator @Inject constructor(private val geocoding: ReverseGeocodingService) {
    private val placed = ConcurrentHashMap<String, MapPoint>()

    suspend fun centre(area: String?, currencyCode: String?): MapPoint {
        val market = marketCentre(currencyCode)
        val query = area?.trim()?.takeIf { it.isNotEmpty() } ?: return market.point
        val key = "${market.countryIso}|$query"
        placed[key]?.let { return it }
        val hit = geocoding.forwardGeocode(query, listOf(market.countryIso)).firstNotNullOfOrNull { usablePoint(it.latitude, it.longitude) }
        return hit?.also { placed[key] = it } ?: market.point
    }
}

internal data class MarketCentre(val countryIso: String, val point: MapPoint)

/** The market a job's currency names, and its main city. */
internal fun marketCentre(currencyCode: String?): MarketCentre = when (currencyCode?.trim()?.uppercase()) {
    "EUR" -> MarketCentre("sk", MapPoint(48.1486, 17.1077))
    else -> MarketCentre("cz", MapPoint(50.0755, 14.4378))
}
