package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus

/**
 * What the server released to *this* caller on this order, read off each field's own arrival.
 *
 * `OrderPiiRedaction` blanks the customer's contact, the door code and the on-job record for a
 * cleaner admitted by the browse gate alone, and the predicate it uses is `CanAccessOrderAsync` —
 * not `isAssignedToCurrentUser`, which is computed from the assignment list and answers a different
 * question. The two disagree for exactly one caller: an employee who booked a cleaning for their own
 * home arrives here as the order's *customer*, so nothing is redacted and the flag is false. Gating
 * the render on the flag hid that person's own data from them, and was a second authorization
 * implementation living beside the server's.
 *
 * This governs **rendering only**. Whether a button, slide or command is offered still reads
 * `isAssignedToCurrentUser` and still fails closed — offering "Slide to start" on a stranger's order
 * is the failure that gate exists to prevent.
 *
 * The redaction is **mixed**, and the roster spans both forms: string scalars are blanked to
 * `string.Empty` (`CustomerName`, `CustomerEmail`, `CustomerPhone`, `ConfirmationCode`), collections
 * to `[]` (`OrderNotes`, `OrderIssues`), and every free-text field is set to `null` —
 * `AccessInstructions`, `Notes`, `SpecialInstructions`, `CompletionNotes`, plus `Address`, `Review`
 * and `PreferredOffer`. Neither `!= null` nor `!= ""` alone covers it, so every read here is
 * arrival-by-content: `isNotBlank` on a scalar, `isNullOrEmpty` on a list.
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
