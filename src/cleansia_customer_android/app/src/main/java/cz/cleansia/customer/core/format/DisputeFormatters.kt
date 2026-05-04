package cz.cleansia.customer.core.format

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import cz.cleansia.customer.ui.theme.StatusCancelledText
import cz.cleansia.customer.ui.theme.SuccessText
import cz.cleansia.customer.ui.theme.WarningStar

/**
 * Color keyed off the backend `DisputeStatus.value`. The backend enum
 * (`src/Cleansia.Core.Domain/Enums/DisputeStatus.cs`) has SIX values, 1-indexed:
 *   1 = Pending             → amber (warning) — freshly filed, awaiting triage
 *   2 = UnderReview         → primary sky — support is actively looking
 *   3 = WaitingForResponse  → primary sky — ping-ponging with the customer
 *   4 = Resolved            → green — happy terminal state
 *   5 = Closed              → neutral slate — closed without refund
 *   6 = Escalated           → red — handed off (e.g. Stripe dispute)
 *   null / unknown          → outlineVariant
 *
 * Note the mismatch with `OrderStatus` which is 0-indexed. Keep this switch
 * keyed on the exact `value` integers the backend serializes.
 *
 * Consumers typically use the returned color as a tint plus a soft-tinted
 * background via `color.copy(alpha = 0.14f)` — same treatment as order pills.
 */
@Composable
fun disputeStatusColor(statusValue: Int?): Color = when (statusValue) {
    1 -> WarningStar                                // Pending
    2, 3 -> MaterialTheme.colorScheme.primary       // UnderReview / WaitingForResponse
    4 -> SuccessText                                // Resolved
    5 -> StatusCancelledText                        // Closed
    6 -> MaterialTheme.colorScheme.error            // Escalated
    else -> MaterialTheme.colorScheme.outlineVariant
}

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
