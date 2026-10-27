package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderItem

/**
 * What finishing a job under way asks of the signed-in cleaner. On a job with two or more cleaners each
 * one marks their own part done, and once every seat still on it is marked any of them completes it; a
 * job with one cleaner on it completes in one step.
 */
sealed interface CompletionStep {
    data object Complete : CompletionStep
    data object CompleteJob : CompletionStep
    data object MarkMyPart : CompletionStep
    data object WaitingForCrew : CompletionStep
}

fun OrderItem.completionStep(myEmployeeId: String?): CompletionStep {
    val seats = assignedEmployees.orEmpty()
    if (seats.size < 2) return CompletionStep.Complete
    if (seats.all { !it.completionConfirmedAt.isNullOrBlank() }) return CompletionStep.CompleteJob
    val mine = seats.firstOrNull { it.employeeId == myEmployeeId }
    return if (mine?.completionConfirmedAt.isNullOrBlank()) CompletionStep.MarkMyPart else CompletionStep.WaitingForCrew
}
