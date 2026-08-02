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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
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
 * Coloured status pill on the order-detail metadata row, mirroring the iOS
 * compact header. Colour tokens come from `ui/theme/Color.kt` and match the
 * customer-app palette for cross-app visual consistency; the label comes from
 * [orderStatusLabel] so the pill and the status timeline can never disagree.
 */
@Composable
fun OrderStatusPill(status: OrderStatus?) {
    val (bg, fg) = when (status) {
        OrderStatus._0, OrderStatus._1 -> StatusPendingBg to StatusPendingText
        OrderStatus._2 -> StatusConfirmedBg to StatusConfirmedText
        OrderStatus._3, OrderStatus._4 -> StatusInProgressBg to StatusInProgressText
        OrderStatus._5 -> StatusCompletedBg to StatusCompletedText
        OrderStatus._6 -> StatusCancelledBg to StatusCancelledText
        null -> Color.LightGray to Color.DarkGray
    }

    Text(
        text = orderStatusLabel(status),
        modifier = Modifier
            .clip(RoundedCornerShape(50))
            .background(bg)
            .padding(horizontal = 10.dp, vertical = 4.dp),
        style = MaterialTheme.typography.labelSmall,
        color = fg,
        fontWeight = FontWeight.SemiBold,
    )
}
