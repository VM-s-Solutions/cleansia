package cz.cleansia.customer.core.location

import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject

data class GeocodedAddress(
    val latitude: Double,
    val longitude: Double,
    val street: String,
    val city: String,
    val zipCode: String,
    val country: String,
    val formatted: String,
)

/**
 * Geocoding via Mapbox Geocoding v5 — reuses the existing MAPBOX_ACCESS_TOKEN
 * we already inject for the map itself, so no extra keys / accounts needed.
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
     * Forward: query text → up to 5 candidate addresses, biased to Czech Republic.
     * Used by the search box in the address picker.
     */
    suspend fun forwardGeocode(query: String): List<GeocodedAddress> =
        withContext(Dispatchers.IO) {
            if (accessToken.isBlank() || query.isBlank()) return@withContext emptyList()
            runCatching {
                val encoded = query.trim().take(120) // Mapbox hard-caps input length
                val url = ("https://api.mapbox.com/geocoding/v5/mapbox.places/$encoded.json").toHttpUrl()
                    .newBuilder()
                    .addQueryParameter("language", "cs")
                    .addQueryParameter("limit", "5")
                    .addQueryParameter("country", "cz")
                    .addQueryParameter("types", "address,place,locality,neighborhood,postcode")
                    .addQueryParameter("autocomplete", "true")
                    .addQueryParameter("access_token", accessToken)
                    .build()
                val request = Request.Builder().url(url).get().build()
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

        var city = ""
        var zip = ""
        var country = ""
        val context = feature.optJSONArray("context")
        if (context != null) {
            for (i in 0 until context.length()) {
                val item = context.getJSONObject(i)
                val id = item.optString("id", "")
                val text = item.optString("text", "")
                when {
                    id.startsWith("postcode") -> zip = text
                    id.startsWith("place") && city.isBlank() -> city = text
                    id.startsWith("locality") && city.isBlank() -> city = text
                    id.startsWith("country") -> country = text
                }
            }
        }

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
            formatted = placeName,
        )
    }

    private companion object {
        const val TAG = "Geocoding"
    }
}
