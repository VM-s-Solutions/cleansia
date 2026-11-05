package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem
import java.time.Instant

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

/** A seat on her crew as the gate list shows it: the given name only, its mark, and the phone while it arrives. */
data class CrewSeat(val givenName: String?, val initials: String, val doneAt: Instant?, val phone: String?, val isMe: Boolean)

/** The crew-mates first, then her own seat. */
fun OrderItem.crewSeats(myEmployeeId: String?): List<CrewSeat> =
    assignedEmployees.orEmpty()
        .map { seat ->
            val names = seat.fullName?.trim()?.split(Regex("\\s+"))?.filter { it.isNotEmpty() }.orEmpty()
            CrewSeat(
                givenName = names.firstOrNull(),
                initials = names.take(2).joinToString("") { it.take(1).uppercase() },
                doneAt = seat.completionConfirmedAt?.takeIf { it.isNotBlank() }?.let { runCatching { Instant.parse(it) }.getOrNull() },
                phone = seat.phoneNumber?.trim()?.takeIf { it.isNotEmpty() },
                isMe = myEmployeeId != null && seat.employeeId == myEmployeeId,
            )
        }
        .sortedBy { it.isMe }

/** The number a crew-mate may be called on: only while her part is open, never her own seat, never a blank phone. */
fun CrewSeat.callNumber(): String? = phone?.takeIf { !isMe && doneAt == null && it.isNotBlank() }

/** "Marta is already on it" for one named crew-mate on a job not taken; otherwise the board's count. */
fun OrderItem.lateJoinCrew(): BoardText? {
    val taken = assignedEmployeesCount ?: assignedEmployees.orEmpty().size
    if (taken <= 0) return null
    val name = crewSeats(null).singleOrNull()?.givenName
    return if (taken == 1 && name != null) {
        BoardText.Res(R.string.detail_mate_on_it, listOf(name))
    } else {
        BoardText.Plural(R.plurals.orders_flag_crew_taken, taken)
    }
}
