package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus

/**
 * What the server released to THIS caller on this order, read off each field's own arrival.
 *
 * **The redaction predicate is the browse gate, NOT `isAssignedToCurrentUser`** — that one is computed
 * from the assignment list and answers a different question. Render off what arrived, never off a flag
 * you derived. -> /flows/execution-and-completion
 */
data class OrderDisclosure(
    val customerPhone: String?,
    val accessInstructions: String?,
    val showsWorkRecord: Boolean,
) {
    val showsCustomerContact: Boolean get() = customerPhone != null

    val showsAccessInstructions: Boolean get() = accessInstructions != null
}

fun OrderItem.orderDisclosure(): OrderDisclosure = OrderDisclosure(
    customerPhone = customerPhone?.takeIf { it.isNotBlank() },
    accessInstructions = accessInstructions?.takeIf { it.isNotBlank() },
    showsWorkRecord = !orderNotes.isNullOrEmpty() || !orderIssues.isNullOrEmpty(),
)

/**
 * The door code's card. The status conjunct answers *when is this useful* — a code is worth showing
 * on the way and on the job — and never *may this caller see it*, which the server already answered
 * by blanking the field. Dropping the status term would leave a door code permanently on screen for
 * a job that finished.
 */
fun OrderDisclosure.showsAccessCard(status: OrderStatus?): Boolean =
    showsAccessInstructions && (status == OrderStatus._3 || status == OrderStatus._4)

/**
 * The notes & issues card. Two independent reasons to draw it: the record arrived, or this caller may
 * start one. The second is why an assignee with an empty record still gets the add buttons.
 */
fun OrderDisclosure.showsWorkRecordSection(canAddNotesOrIssues: Boolean): Boolean =
    showsWorkRecord || canAddNotesOrIssues
