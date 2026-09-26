package cz.cleansia.customer.core.booking

/**
 * Whether a booking may be paid in cash — `BookingPolicy.AllowsCash`: a signed-in customer, on a booking
 * the server says one cleaner does alone. The crew is the quote's `requiredEmployees` for the selection on
 * screen, never a client estimate; null means no quote describes that selection yet. The server re-decides
 * on create, so this only decides what the payment step offers.
 */
sealed interface CashEligibility {
    data object Available : CashEligibility

    data object NeedsAccount : CashEligibility

    data class NeedsCard(val requiredCleaners: Int) : CashEligibility

    data object Pending : CashEligibility

    /** A verdict that takes cash away. An unknown crew is not one — the choice is kept until it is known. */
    val isRefused: Boolean get() = this is NeedsAccount || this is NeedsCard

    companion object {
        fun resolve(signedIn: Boolean, requiredEmployees: Int?): CashEligibility = when {
            requiredEmployees != null && requiredEmployees > 1 -> NeedsCard(requiredEmployees)
            !signedIn -> NeedsAccount
            requiredEmployees == 1 -> Available
            else -> Pending
        }
    }
}
