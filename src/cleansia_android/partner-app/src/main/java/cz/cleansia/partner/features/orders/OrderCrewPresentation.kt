package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderItem

/**
 * How many cleaners the job needs and whether a seat is still open —
 * `TakeOrder`'s free-seat conjunct refuses a take, and neither number is
 * derivable client-side.
 *
 * Every member is read off the wire; none is computed from another. The server
 * sends both the count and the flag from one source, and a client that reads
 * one shape and derives the other is how the two come to disagree.
 */
sealed interface OrderCrew {
    val crewSize: Int

    data class SpotsOpen(override val crewSize: Int, val openSpots: Int) : OrderCrew
    data class Full(override val crewSize: Int) : OrderCrew
}

/** Null when the wire carried no seat block — a server that predates it. */
fun OrderItem.orderCrew(): OrderCrew? {
    val crewSize = requiredEmployees?.takeIf { it > 0 } ?: return null
    val openSpots = availableSpots ?: 0
    return if (hasAvailableSpots == true && openSpots > 0) {
        OrderCrew.SpotsOpen(crewSize, openSpots)
    } else {
        OrderCrew.Full(crewSize)
    }
}
