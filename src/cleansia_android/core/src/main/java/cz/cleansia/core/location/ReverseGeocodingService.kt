package cz.cleansia.core.location

import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject

/**
 * Geocoding via Mapbox Geocoding v5. Forward (text → addresses) and
 * reverse (lat/lng → address). Both apps share this — consumer Hilt
 * modules provide the [accessToken] from their respective BuildConfig
 * so :core stays token-agnostic.
 *
 * API docs: https://docs.mapbox.com/api/search/geocoding-v5/
 */
class ReverseGeocodingService(
    private val httpClient: OkHttpClient,
    private val accessToken: String,
) {
    /** Reverse: lat/lng → GeocodedAddress. Returns null on network/parse error. */
    suspend fun reverseGeocode(lat: Double, lng: Double): GeocodedAddress? =
        withContext(Dispatchers.IO) {
            if (accessToken.isBlank()) return@withContext null
            runCatching {
                val url = "https://api.mapbox.com/geocoding/v5/mapbox.places/$lng,$lat.json".toHttpUrl()
                    .newBuilder()
                    .addQueryParameter("language", "cs")
                    .addQueryParameter("limit", "1")
                    .addQueryParameter("types", "address,place,locality,neighborhood")
                    .addQueryParameter("access_token", accessToken)
                    .build()
                val request = Request.Builder().url(url).get().build()
                httpClient.newCall(request).execute().use { response ->
                    if (!response.isSuccessful) return@use null
                    val body = response.body?.string() ?: return@use null
                    parseSingle(body, lat, lng)
                }
            }.onFailure { Log.w(TAG, "Reverse lookup failed", it) }.getOrNull()
        }

    /**
     * Forward: query text → up to 5 candidate addresses, biased to the
     * supplied list of ISO country codes (lowercase). The caller is
     * expected to source these from the relevant `/Country/GetServiced`
     * endpoint so the bias stays in lock-step with which countries the
     * company actually operates in. Empty list = no bias (Mapbox
     * returns global suggestions).
     */
    suspend fun forwardGeocode(
        query: String,
        countryIsoCodes: List<String> = listOf("cz"),
    ): List<GeocodedAddress> =
        withContext(Dispatchers.IO) {
            if (accessToken.isBlank() || query.isBlank()) return@withContext emptyList()
            runCatching {
                val encoded = query.trim().take(120) // Mapbox hard-caps input length
                val builder = ("https://api.mapbox.com/geocoding/v5/mapbox.places/$encoded.json").toHttpUrl()
                    .newBuilder()
                    .addQueryParameter("language", "cs")
                    .addQueryParameter("limit", "5")
                    .addQueryParameter("types", "address,place,locality,neighborhood,postcode")
                    .addQueryParameter("autocomplete", "true")
                    .addQueryParameter("access_token", accessToken)
                if (countryIsoCodes.isNotEmpty()) {
                    builder.addQueryParameter(
                        "country",
                        countryIsoCodes.joinToString(",") { it.lowercase() },
                    )
                }
                val request = Request.Builder().url(builder.build()).get().build()
                httpClient.newCall(request).execute().use { response ->
                    if (!response.isSuccessful) return@use emptyList()
                    val body = response.body?.string() ?: return@use emptyList()
                    parseMany(body)
                }
            }.onFailure { Log.w(TAG, "Forward lookup failed", it) }.getOrDefault(emptyList())
        }

    private fun parseSingle(body: String, lat: Double, lng: Double): GeocodedAddress? {
        val root = JSONObject(body)
        val features = root.optJSONArray("features") ?: return null
        if (features.length() == 0) return null
        return featureToAddress(features.getJSONObject(0), fallbackLat = lat, fallbackLng = lng)
    }

    private fun parseMany(body: String): List<GeocodedAddress> {
        val root = JSONObject(body)
        val features = root.optJSONArray("features") ?: return emptyList()
        return (0 until features.length()).mapNotNull { i ->
            featureToAddress(features.getJSONObject(i))
        }
    }

    /**
     * Mapbox feature shape:
     *  - center: [lng, lat]
     *  - text: street base (e.g. "Vinohradská")
     *  - address: house number (e.g. "12")
     *  - place_name: full formatted
     *  - context[]: postcode / place / locality / country
     */
    private fun featureToAddress(
        feature: JSONObject,
        fallbackLat: Double? = null,
        fallbackLng: Double? = null,
    ): GeocodedAddress? {
        val placeName = feature.optString("place_name", "")
        val baseStreet = feature.optString("text", "")
        val houseNumber = feature.optString("address", "")

        val street = when {
            baseStreet.isNotBlank() && houseNumber.isNotBlank() -> "$baseStreet $houseNumber"
            baseStreet.isNotBlank() -> baseStreet
            else -> placeName.substringBefore(",").trim()
        }

        // Mapbox returns context items in most-specific-first order, so
        // for Prague the array is [locality.holešovice, place.praha, …].
        // The serviced-city list stores the city name (`Praha`), not the
        // district (`Holešovice`). Always prefer `place` over `locality`
        // — `locality` is a fallback for results that have no `place` row
        // at all.
        var cityFromPlace = ""
        var cityFromLocality = ""
        var zip = ""
        var country = ""
        var countryIsoCode = ""
        val context = feature.optJSONArray("context")
        if (context != null) {
            for (i in 0 until context.length()) {
                val item = context.getJSONObject(i)
                val id = item.optString("id", "")
                val text = item.optString("text", "")
                when {
                    id.startsWith("postcode") -> zip = text
                    id.startsWith("place") && cityFromPlace.isBlank() -> cityFromPlace = text
                    id.startsWith("locality") && cityFromLocality.isBlank() -> cityFromLocality = text
                    id.startsWith("country") -> {
                        country = text
                        // Mapbox returns ISO codes as short_code; cz, sk, etc.
                        countryIsoCode = item.optString("short_code", "").lowercase()
                    }
                }
            }
        }
        val city = cityFromPlace.ifBlank { cityFromLocality }

        val center = feature.optJSONArray("center")
        val lng = center?.optDouble(0) ?: fallbackLng ?: return null
        val lat = center?.optDouble(1) ?: fallbackLat ?: return null

        return GeocodedAddress(
            latitude = lat,
            longitude = lng,
            street = street,
            city = city,
            zipCode = zip,
            country = country,
            countryIsoCode = countryIsoCode,
            formatted = placeName,
        )
    }

    private companion object {
        const val TAG = "Geocoding"
    }
}
