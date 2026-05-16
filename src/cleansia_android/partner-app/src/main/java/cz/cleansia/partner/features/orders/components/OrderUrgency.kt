package cz.cleansia.partner.features.orders.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.orders.OrderStatus
import java.time.Duration
import java.time.LocalDateTime
import java.time.format.DateTimeFormatter

enum class UrgencyLevel { NONE, LOW, MEDIUM, HIGH, OVERDUE }

data class OrderUrgencyInfo(
    val level: UrgencyLevel,
    val chipText: String?,
    val accentColor: Color?
)

/**
 * Computes urgency info from the order's cleaning date/time.
 * Only PENDING and CONFIRMED orders show urgency indicators.
 */
@Composable
fun rememberOrderUrgency(
    cleaningDateTime: String?,
    orderStatus: OrderStatus
): OrderUrgencyInfo {
    // Pre-resolve string resources
    val overdueText = stringResource(R.string.urgency_overdue)

    return remember(cleaningDateTime, orderStatus) {
        computeUrgency(cleaningDateTime, orderStatus, overdueText)
    }
}

private fun computeUrgency(
    cleaningDateTime: String?,
    orderStatus: OrderStatus,
    overdueText: String
): OrderUrgencyInfo {
    val none = OrderUrgencyInfo(UrgencyLevel.NONE, null, null)

    if (orderStatus != OrderStatus.PENDING && orderStatus != OrderStatus.CONFIRMED) {
        return none
    }
    if (cleaningDateTime.isNullOrBlank()) {
        return none
    }

    val scheduledDateTime = try {
        LocalDateTime.parse(cleaningDateTime, DateTimeFormatter.ISO_DATE_TIME)
    } catch (_: Exception) {
        try {
            LocalDateTime.parse(cleaningDateTime.replace("Z", ""), DateTimeFormatter.ISO_LOCAL_DATE_TIME)
        } catch (_: Exception) {
            return none
        }
    }

    val now = LocalDateTime.now()
    val duration = Duration.between(now, scheduledDateTime)
    val totalMinutes = duration.toMinutes()

    return when {
        duration.isNegative -> OrderUrgencyInfo(
            level = UrgencyLevel.OVERDUE,
            chipText = overdueText,
            accentColor = Color(0xFFEF4444)
        )
        totalMinutes < 60 -> OrderUrgencyInfo(
            level = UrgencyLevel.HIGH,
            chipText = "${totalMinutes}m",
            accentColor = Color(0xFFEF4444)
        )
        totalMinutes < 240 -> { // <4h
            val hours = totalMinutes / 60
            val mins = totalMinutes % 60
            OrderUrgencyInfo(
                level = UrgencyLevel.MEDIUM,
                chipText = "${hours}h ${mins}m",
                accentColor = Color(0xFFF59E0B)
            )
        }
        totalMinutes < 1440 -> { // <24h
            val hours = totalMinutes / 60
            OrderUrgencyInfo(
                level = UrgencyLevel.LOW,
                chipText = "${hours}h",
                accentColor = null
            )
        }
        else -> none
    }
}

/**
 * Small urgency chip that shows time-until-scheduled.
 * Returns nothing if urgency is NONE.
 */
@Composable
fun UrgencyChip(urgency: OrderUrgencyInfo) {
    if (urgency.chipText == null) return

    val chipBgColor = when (urgency.level) {
        UrgencyLevel.OVERDUE, UrgencyLevel.HIGH -> MaterialTheme.colorScheme.error.copy(alpha = 0.12f)
        UrgencyLevel.MEDIUM -> Color(0xFFF59E0B).copy(alpha = 0.12f)
        UrgencyLevel.LOW -> MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)
        UrgencyLevel.NONE -> Color.Transparent
    }

    val chipTextColor = when (urgency.level) {
        UrgencyLevel.OVERDUE, UrgencyLevel.HIGH -> MaterialTheme.colorScheme.error
        UrgencyLevel.MEDIUM -> Color(0xFFB45309) // dark amber
        UrgencyLevel.LOW -> MaterialTheme.colorScheme.primary
        UrgencyLevel.NONE -> Color.Transparent
    }

    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(12.dp))
            .background(chipBgColor)
            .padding(horizontal = 8.dp, vertical = 4.dp)
    ) {
        Text(
            text = urgency.chipText,
            style = MaterialTheme.typography.labelSmall,
            fontWeight = FontWeight.SemiBold,
            color = chipTextColor
        )
    }
}
