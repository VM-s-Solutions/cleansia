package cz.cleansia.partner.features.orders

import androidx.annotation.StringRes
import cz.cleansia.partner.R
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

    /** The district or the city — enough to judge the travel, nothing to navigate to. */
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

/** The access mode as the wizard wrote it (`CreateOrder`: at_home, keys_handover, door_code, reception); any other is left out. */
@StringRes
fun accessModeRes(mode: String?): Int? = when (mode) {
    "at_home" -> R.string.access_mode_at_home
    "keys_handover" -> R.string.access_mode_keys_handover
    "door_code" -> R.string.access_mode_door_code
    "reception" -> R.string.access_mode_reception
    else -> null
}

/** "Customer will be home · Floor 3 · flat 12": each piece left out when it did not arrive. */
fun OrderItem.accessFacts(): List<BoardText> = listOfNotNull(
    accessModeRes(accessMode)?.let { BoardText.Res(it) },
    customerFloor?.trim()?.takeIf { it.isNotEmpty() }?.let { BoardText.Res(R.string.detail_floor, listOf(it)) },
    customerApartment?.trim()?.takeIf { it.isNotEmpty() }?.let { BoardText.Res(R.string.detail_flat, listOf(it)) },
)
