package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.partner.api.model.OrderItem

/**
 * Order metadata row that sits inside the sheet content (NOT in the
 * drag handle anymore). Two rows:
 *
 *   #ORD-…                                 [1200 CZK]
 *   📅 May 22, 2026, 9:00 AM
 *
 * Order # on the left, price pill on the right. Date with calendar
 * icon below. Renders as a transparent inline element — no card
 * background, no shadow — so it reads as metadata between the timer
 * card above and the progress bar below.
 */
@Composable
fun OrderMetadataRow(
    order: OrderItem,
    modifier: Modifier = Modifier,
) {
    val dateLabel = order.cleaningDateTime
        ?.takeIf { it.isNotBlank() }
        ?.let { formatOrderDateTime(it) }
    val currencyCode = order.currency?.code ?: order.currency?.symbol
    val payLabel = order.estimatedCleanerPay
        ?.takeIf { it > 0.0 }
        ?.let { formatOrderPrice(it, currencyCode) }

    Column(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = 4.dp),
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(
                text = "#${order.displayOrderNumber ?: order.id?.take(8) ?: "—"}",
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (payLabel != null) {
                PayChip(label = payLabel)
            }
        }
        if (dateLabel != null) {
            Spacer(Modifier.height(4.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Outlined.CalendarToday,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(14.dp),
                )
                Spacer(Modifier.width(6.dp))
                Text(
                    text = dateLabel,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
private fun PayChip(label: String) {
    Surface(
        shape = RoundedCornerShape(999.dp),
        color = MaterialTheme.colorScheme.primaryContainer,
    ) {
        Text(
            text = label,
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
            style = MaterialTheme.typography.labelLarge.copy(
                fontWeight = FontWeight.ExtraBold,
                letterSpacing = (-0.2).sp,
            ),
            color = MaterialTheme.colorScheme.onPrimaryContainer,
        )
    }
}
