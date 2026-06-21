package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.History
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus

/**
 * Vertical status timeline showing each step the order has been through
 * plus the current step highlighted in the brand color. Backend feeds
 * `statusHistory` as a list of (status, createdOn) entries — we sort
 * by timestamp ascending and render one row per entry. The most recent
 * (= the current status) gets the brand-color filled dot; previously-
 * completed steps get a green check; if a future step is implied by
 * the lifecycle (e.g. Confirmed → not yet OnTheWay → not yet Started)
 * we don't render it. We only show what actually happened.
 */
@Composable
fun StatusTimeline(
    order: OrderItem,
    modifier: Modifier = Modifier,
) {
    val history = order.statusHistory.orEmpty()
        .filter { !it.createdOn.isNullOrBlank() }
        .sortedBy { it.createdOn }
    if (history.isEmpty()) return

    OrderSectionCard(
        title = stringResource(R.string.status_timeline_section_title),
        icon = Icons.Outlined.History,
        modifier = modifier,
    ) {
        Column(verticalArrangement = Arrangement.spacedBy(0.dp)) {
            history.forEachIndexed { index, entry ->
                val isLast = index == history.lastIndex
                TimelineRow(
                    label = labelForStatusName(entry.status?.name, entry.status?.value),
                    timestamp = formatOrderDateTime(entry.createdOn) ?: "—",
                    isCurrent = isLast,
                    isLastInList = isLast,
                )
            }
        }
    }
}

@Composable
private fun TimelineRow(
    label: String,
    timestamp: String,
    isCurrent: Boolean,
    isLastInList: Boolean,
) {
    Row(modifier = Modifier.fillMaxWidth()) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier.padding(top = 2.dp),
        ) {
            TimelineDot(isCurrent = isCurrent)
            if (!isLastInList) {
                Box(
                    modifier = Modifier
                        .width(2.dp)
                        .height(28.dp)
                        .background(MaterialTheme.colorScheme.outlineVariant),
                )
            }
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.padding(bottom = if (isLastInList) 0.dp else 12.dp)) {
            Text(
                text = label,
                style = MaterialTheme.typography.bodyMedium.copy(
                    fontWeight = if (isCurrent) FontWeight.SemiBold else FontWeight.Medium,
                ),
                color = if (isCurrent) MaterialTheme.colorScheme.onSurface
                    else MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                text = timestamp,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun TimelineDot(isCurrent: Boolean) {
    val primary = MaterialTheme.colorScheme.primary
    val past = Color(0xFF16A34A)
    if (isCurrent) {
        Box(
            modifier = Modifier
                .size(20.dp)
                .clip(CircleShape)
                .background(primary),
        )
    } else {
        Box(
            modifier = Modifier
                .size(20.dp)
                .clip(CircleShape)
                .background(past),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = Icons.Outlined.Check,
                contentDescription = null,
                tint = Color.White,
                modifier = Modifier.size(12.dp),
            )
        }
    }
}

/**
 * Backend ships status names as enum-friendly strings ("New",
 * "Confirmed", "OnTheWay", "InProgress", "Completed", "Cancelled");
 * we use them verbatim with light prettification. Fallback walks the
 * numeric value in case future seed data drops the name.
 */
private fun labelForStatusName(name: String?, value: Int?): String {
    val resolved = name?.takeIf { it.isNotBlank() }
        ?: when (value) {
            OrderStatus._0.value -> "New"
            OrderStatus._1.value -> "Pending"
            OrderStatus._2.value -> "Confirmed"
            OrderStatus._3.value -> "OnTheWay"
            OrderStatus._4.value -> "InProgress"
            OrderStatus._5.value -> "Completed"
            else -> "—"
        }
    // "OnTheWay" → "On the way", "InProgress" → "In progress" for readability.
    return resolved
        .replace(Regex("([a-z])([A-Z])"), "$1 $2")
        .replaceFirstChar { it.uppercase() }
}
