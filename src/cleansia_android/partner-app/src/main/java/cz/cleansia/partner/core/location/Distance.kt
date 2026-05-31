package cz.cleansia.partner.core.location

import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.pow
import kotlin.math.sin
import kotlin.math.sqrt

/** Earth's mean radius in kilometres — Haversine's classic constant. */
private const val EARTH_RADIUS_KM = 6371.0088

/**
 * Great-circle distance between two coordinates in kilometres. Accurate to
 * ~0.5% for the distances we care about (job is within driving range of the
 * cleaner), so good enough for the "12 km away" badge — we don't need
 * routing-grade precision.
 */
fun haversineKm(
    fromLatitude: Double,
    fromLongitude: Double,
    toLatitude: Double,
    toLongitude: Double,
): Double {
    val dLat = Math.toRadians(toLatitude - fromLatitude)
    val dLon = Math.toRadians(toLongitude - fromLongitude)
    val a = sin(dLat / 2).pow(2) +
        cos(Math.toRadians(fromLatitude)) * cos(Math.toRadians(toLatitude)) *
        sin(dLon / 2).pow(2)
    val c = 2 * atan2(sqrt(a), sqrt(1 - a))
    return EARTH_RADIUS_KM * c
}
