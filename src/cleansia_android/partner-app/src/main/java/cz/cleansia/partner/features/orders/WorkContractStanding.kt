package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus

/**
 * Where the signed-in cleaner stands on this job's contract for work.
 *
 * The caller's row is the acceptance whose `orderEmployeeId` is their own crew entry's id — the
 * server sends the acceptances without names, and the crew entry is where the (already masked)
 * identity lives. A crew entry with no matching acceptance IS the pending state: the only way onto a
 * crew without accepting is an administrator's placement.
 */
sealed interface WorkContractStanding {
    data object None : WorkContractStanding

    /** On the crew, no acceptance for the seat, and the job is not over — the banner. */
    data object Pending : WorkContractStanding
    data class Accepted(
        val acceptanceId: String,
        val acceptedOn: String,
        val documentVersion: String,
    ) : WorkContractStanding
}

fun OrderItem.workContractStanding(myEmployeeId: String?): WorkContractStanding {
    if (myEmployeeId == null) return WorkContractStanding.None
    val seat = assignedEmployees.orEmpty().firstOrNull { it.employeeId == myEmployeeId } ?: return WorkContractStanding.None
    val seatId = seat.id ?: return WorkContractStanding.None
    val row = workContractAcceptances.orEmpty().firstOrNull { it.orderEmployeeId == seatId }
    if (row != null) {
        val id = row.id ?: return WorkContractStanding.None
        val acceptedOn = row.acceptedOn ?: return WorkContractStanding.None
        return WorkContractStanding.Accepted(id, acceptedOn, row.documentVersion.orEmpty())
    }
    return if (orderStatus.toOrderStatus() in ACCEPTABLE_STATUSES) WorkContractStanding.Pending else WorkContractStanding.None
}

/** Mirrors `OrderAvailability.OfferableStatuses`: the statuses under which the standalone acceptance is allowed. */
private val ACCEPTABLE_STATUSES = setOf(OrderStatus._0, OrderStatus._2, OrderStatus._3, OrderStatus._4)
