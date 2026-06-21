package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.ui.theme.StatusCancelledBg
import cz.cleansia.partner.ui.theme.StatusCancelledText
import cz.cleansia.partner.ui.theme.StatusCompletedBg
import cz.cleansia.partner.ui.theme.StatusCompletedText
import cz.cleansia.partner.ui.theme.StatusConfirmedBg
import cz.cleansia.partner.ui.theme.StatusConfirmedText
import cz.cleansia.partner.ui.theme.StatusInProgressBg
import cz.cleansia.partner.ui.theme.StatusInProgressText
import cz.cleansia.partner.ui.theme.StatusPendingBg
import cz.cleansia.partner.ui.theme.StatusPendingText

/**
 * Coloured status pill used in list rows + the details header. Colour
 * tokens come from `ui/theme/Color.kt` and match the customer-app palette
 * for cross-app visual consistency.
 *
 * Backend status integer mapping (kept here so the UI is the single source
 * of truth for "what does status N mean to a partner cleaner"):
 *   0 New, 1 Pending, 2 Confirmed, 3 OnTheWay, 4 InProgress,
 *   5 Completed, 6 Cancelled.
 */
/** Convert a backend `Code` (carries the integer value) to the typed enum. */
fun Code?.toOrderStatus(): OrderStatus? = this?.value?.let { v ->
    OrderStatus.values().firstOrNull { it.value == v }
}

@Composable
fun OrderStatusPill(status: OrderStatus?) {
    val (label, bg, fg) = when (status) {
        OrderStatus._0 -> Triple(stringResource(R.string.status_new), StatusPendingBg, StatusPendingText)
        OrderStatus._1 -> Triple(stringResource(R.string.status_pending), StatusPendingBg, StatusPendingText)
        OrderStatus._2 -> Triple(stringResource(R.string.status_confirmed), StatusConfirmedBg, StatusConfirmedText)
        OrderStatus._3 -> Triple(stringResource(R.string.status_on_the_way), StatusInProgressBg, StatusInProgressText)
        OrderStatus._4 -> Triple(stringResource(R.string.status_in_progress), StatusInProgressBg, StatusInProgressText)
        OrderStatus._5 -> Triple(stringResource(R.string.status_completed), StatusCompletedBg, StatusCompletedText)
        OrderStatus._6 -> Triple(stringResource(R.string.status_cancelled), StatusCancelledBg, StatusCancelledText)
        null -> Triple("—", Color.LightGray, Color.DarkGray)
    }

    Text(
        text = label,
        modifier = Modifier
            .clip(RoundedCornerShape(50))
            .background(bg)
            .padding(horizontal = 10.dp, vertical = 4.dp),
        style = MaterialTheme.typography.labelSmall,
        color = fg,
        fontWeight = FontWeight.SemiBold,
    )
}
