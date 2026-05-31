package cz.cleansia.core.location

/**
 * Simple lat/lng pair used as the canonical "where is the user" type
 * across both apps. Pre-extraction the customer-app called this
 * `UserLocation` and partner-app called it `LatLng`; unified here so a
 * shared service doesn't have to translate between two equivalent
 * shapes.
 *
 * Coordinates are WGS84 (the default Mapbox / GPS reference frame).
 */
data class UserLocation(
    val latitude: Double,
    val longitude: Double,
)
