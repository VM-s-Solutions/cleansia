package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus

/**
 * What the order's location resolved to for *this* caller.
 *
 * The server nulls `address` for a caller the order does not belong to and
 * sends the coarse `customerAddressApproximate` zone to everyone, so the
 * arrival of a street address is the discriminator. It is deliberately not
 * `isAssignedToCurrentUser`: an employee who booked a cleaning for their own
 * home reaches this screen as the order's *customer*, with a full address and
 * that flag false, and gating on it would hide their own data from them —
 * a second authorization implementation next to the server's.
 */
sealed interface OrderLocation {
    val line: String?

    data class Precise(
        override val line: String,
        val latitude: Double?,
        val longitude: Double?,
    ) : OrderLocation

    /** City + ZIP prefix — enough to judge the travel, nothing to navigate to. */
    data class Approximate(override val line: String) : OrderLocation

    data object None : OrderLocation {
        override val line: String? = null
    }
}

fun OrderItem.orderLocation(): OrderLocation {
    val street = address.formatSingleLine()
    if (street != null) return OrderLocation.Precise(street, address?.latitude, address?.longitude)
    // BuildApproximateAddress sends "" — not null — for an order with no city.
    val zone = customerAddressApproximate?.takeIf { it.isNotBlank() } ?: return OrderLocation.None
    return OrderLocation.Approximate(zone)
}

/** Navigation needs a door, not a district. */
fun OrderLocation.navigationTarget(): OrderLocation.Precise? = this as? OrderLocation.Precise

/**
 * Where the map backdrop centres, or null when there is nothing precise to
 * point at. Completed keeps it — the location is still context worth having —
 * and only Cancelled drops it, since the visit never happened.
 */
fun OrderLocation.mapPoint(status: OrderStatus?): Pair<Double, Double>? {
    val target = navigationTarget()?.takeIf { status != OrderStatus._6 } ?: return null
    val latitude = target.latitude ?: return null
    val longitude = target.longitude ?: return null
    return latitude to longitude
}
