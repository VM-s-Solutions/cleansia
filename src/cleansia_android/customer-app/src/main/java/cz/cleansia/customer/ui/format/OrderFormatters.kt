package cz.cleansia.customer.ui.format

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import cz.cleansia.customer.ui.theme.StatusCancelledText
import cz.cleansia.customer.ui.theme.StatusCompletedText
import cz.cleansia.customer.ui.theme.StatusConfirmedBg
import cz.cleansia.customer.ui.theme.StatusInProgressBg
import cz.cleansia.customer.ui.theme.WarningStar

/**
 * Compose-typed formatting helpers for Order DTOs. Lives in `ui/` because it
 * reaches into `MaterialTheme` / theme colors — not safe to depend on from a
 * ViewModel. Pure-Kotlin Order helpers (date, price formatting) live in
 * [cz.cleansia.customer.core.format].
 */

/**
 * Color keyed off backend `OrderStatus.value`:
 *   New=0 / Pending=1   → amber (warning)
 *   Confirmed=2         → primary-solid
 *   OnTheWay=3          → light blue (cleaner en route)
 *   InProgress=4        → light blue
 *   Completed=5         → green success
 *   Cancelled=6         → neutral slate text
 */
@Composable
fun orderStatusColor(statusValue: Int?): Color = when (statusValue) {
    0, 1 -> WarningStar
    2 -> StatusConfirmedBg
    3, 4 -> StatusInProgressBg
    5 -> StatusCompletedText
    6 -> StatusCancelledText
    else -> MaterialTheme.colorScheme.outlineVariant
}
