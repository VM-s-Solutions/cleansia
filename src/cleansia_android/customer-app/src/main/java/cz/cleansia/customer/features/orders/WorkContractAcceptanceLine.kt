package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.orders.OrderDetailDto

/** One crew member's acceptance of the contract for work, as the customer's detail states it. */
data class WorkContractAcceptanceLine(
    val id: String,
    /** The crew entry's name as the server masked it; null when it sent none. */
    val cleanerName: String?,
    /** ISO-8601 date-time. */
    val acceptedOn: String,
    val documentVersion: String,
)

/**
 * The acceptance carries no name; the crew entry whose id is the acceptance's seat does, already
 * masked for the customer's eyes. Pairing them here keeps one masking path. An acceptance naming no
 * current seat, or missing what the line reads, is dropped — a line that cannot say who or when says
 * nothing the crew card does not.
 */
fun OrderDetailDto.workContractAcceptanceLines(): List<WorkContractAcceptanceLine> {
    val crew = assignedEmployees.orEmpty()
    return workContractAcceptances.orEmpty().mapNotNull { acceptance ->
        val seat = crew.firstOrNull { it.id != null && it.id == acceptance.orderEmployeeId }
            ?: return@mapNotNull null
        WorkContractAcceptanceLine(
            id = acceptance.id ?: return@mapNotNull null,
            cleanerName = seat.fullName?.takeIf { it.isNotBlank() },
            acceptedOn = acceptance.acceptedOn ?: return@mapNotNull null,
            documentVersion = acceptance.documentVersion.orEmpty(),
        )
    }
}
