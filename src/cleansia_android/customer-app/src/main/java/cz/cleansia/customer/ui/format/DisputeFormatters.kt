package cz.cleansia.customer.ui.format

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import cz.cleansia.customer.ui.theme.StatusCancelledText
import cz.cleansia.customer.ui.theme.SuccessText
import cz.cleansia.customer.ui.theme.WarningStar

/**
 * Colour keyed off the backend dispute status VALUE, which is 1-indexed — amber while pending, sky
 * while in review or awaiting a reply, green resolved, slate closed, red rejected.
 *
 * **Keyed on the numeric value, not the name**, because the name is not on the wire.
 * -> /flows/cancellation-refund-dispute
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
