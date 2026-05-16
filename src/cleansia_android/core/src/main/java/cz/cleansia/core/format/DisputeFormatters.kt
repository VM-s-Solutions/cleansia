package cz.cleansia.core.format

/**
 * Pure-Kotlin helpers for Dispute DTOs. Compose-typed helpers (anything
 * returning a `Color`) live in [cz.cleansia.customer.ui.format] so this
 * package stays framework-pure and safe to depend on from ViewModels.
 */

/**
 * True when the dispute is in a state that accepts new customer messages.
 * Pending + UnderReview + WaitingForResponse are all "live"; Resolved /
 * Closed / Escalated are terminal from the customer's perspective.
 *
 * Null (unknown/missing from wire) defaults to allowing messages — the
 * backend will reject if disallowed; surfacing an input is the safer default.
 */
fun disputeAllowsMessages(statusValue: Int?): Boolean = when (statusValue) {
    4, 5, 6 -> false
    else -> true
}
