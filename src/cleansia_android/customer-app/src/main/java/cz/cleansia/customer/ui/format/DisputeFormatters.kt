package cz.cleansia.customer.ui.format

import androidx.compose.material3.ColorScheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.customer.ui.theme.Amber800
import cz.cleansia.customer.ui.theme.Green400
import cz.cleansia.customer.ui.theme.Green800
import cz.cleansia.customer.ui.theme.Slate300
import cz.cleansia.customer.ui.theme.Slate600
import cz.cleansia.customer.ui.theme.WarningStar

/**
 * Colour keyed off the backend dispute status VALUE, which is 1-indexed — amber while pending, sky
 * while in review or awaiting a reply, green resolved, slate closed, red escalated.
 *
 * **Keyed on the numeric value, not the name**, because the name is not on the wire.
 * -> /flows/cancellation-refund-dispute
 */
@Composable
fun disputeStatusColor(statusValue: Int?): Color = disputeStatusInk(statusValue, MaterialTheme.colorScheme)

/**
 * The pill's ink for [scheme] — its label, and the 14 % wash behind it. Each reads 4.5:1 or more on its own
 * wash over the card in both schemes. The amber the pill used to share with the rating star was 1.93:1 in
 * light mode, and the green and slate it took were under 4.5:1 in light mode and under 3:1 in dark, so
 * light mode takes a darker step and dark mode a lighter one; the in-review blue and the escalated red
 * already cleared it.
 */
internal fun disputeStatusInk(statusValue: Int?, scheme: ColorScheme): Color {
    val dark = scheme.surface.luminance() < 0.5f
    return when (statusValue) {
        1 -> if (dark) WarningStar else Amber800    // Pending
        2, 3 -> scheme.primaryText                  // UnderReview / WaitingForResponse
        4 -> if (dark) Green400 else Green800       // Resolved
        6 -> scheme.error                           // Escalated
        else -> if (dark) Slate300 else Slate600    // Closed, and a status this build does not know
    }
}
