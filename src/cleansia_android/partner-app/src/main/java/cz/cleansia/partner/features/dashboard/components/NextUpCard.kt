package cz.cleansia.partner.features.dashboard.components

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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.core.utils.CurrencyUtils
import cz.cleansia.partner.core.utils.DateTimeUtils
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import cz.cleansia.partner.ui.components.clickableWithHaptic
import cz.cleansia.partner.ui.theme.LocalDarkTheme
import java.time.Duration
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime
import java.time.format.DateTimeFormatter

@Composable
internal fun NextUpCard(
    order: UpcomingOrder,
    onClick: () -> Unit
) {
    val isDarkTheme = LocalDarkTheme.current
    val containerColor = if (isDarkTheme) Color(0xFF1A2740) else Color(0xFFEFF6FF)
    val accentColor = if (isDarkTheme) Color(0xFF60A5FA) else Color(0xFF2563EB)

    // Compute time label
    val timeLabel = remember(order.scheduledDate, order.scheduledTime) {
        computeTimeLabel(order.scheduledDate, order.scheduledTime)
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickableWithHaptic { onClick() },
        colors = CardDefaults.cardColors(containerColor = containerColor),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .size(32.dp)
                            .clip(CircleShape)
                            .background(accentColor.copy(alpha = 0.15f)),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.Default.AccessTime,
                            contentDescription = null,
                            tint = accentColor,
                            modifier = Modifier.size(18.dp)
                        )
                    }
                    Spacer(modifier = Modifier.width(8.dp))
                    Column {
                        Text(
                            text = stringResource(R.string.next_up),
                            style = MaterialTheme.typography.labelMedium,
                            fontWeight = FontWeight.Bold,
                            color = accentColor
                        )
                        if (timeLabel != null) {
                            Text(
                                text = timeLabel,
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }

                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = CurrencyUtils.formatCurrency(order.totalAmount, order.currency),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = accentColor
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                        contentDescription = null,
                        tint = accentColor.copy(alpha = 0.6f),
                        modifier = Modifier.size(18.dp)
                    )
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Order details
            Text(
                text = "#${order.orderNumber ?: order.id.take(8)}",
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(4.dp))

            order.customerName?.let { name ->
                Text(
                    text = name,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }

            order.address?.let { address ->
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.LocationOn,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(14.dp)
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = address,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }

            order.servicesPreview?.let { services ->
                Spacer(modifier = Modifier.height(6.dp))
                ServiceChip(serviceName = services)
            }
        }
    }
}

private fun computeTimeLabel(scheduledDate: String?, scheduledTime: String?): String? {
    if (scheduledDate == null) return null
    return try {
        val date = LocalDate.parse(scheduledDate, DateTimeFormatter.ofPattern("yyyy-MM-dd"))
        val today = LocalDate.now()
        val time = scheduledTime?.let {
            try { LocalTime.parse(it) } catch (_: Exception) { null }
        }

        val timeStr = time?.format(DateTimeFormatter.ofPattern("HH:mm")) ?: ""

        when {
            date.isEqual(today) && time != null -> {
                val now = LocalDateTime.now()
                val scheduled = LocalDateTime.of(date, time)
                val diff = Duration.between(now, scheduled)
                if (diff.isNegative) "Today at $timeStr"
                else {
                    val hours = diff.toHours()
                    val minutes = diff.toMinutes() % 60
                    when {
                        hours > 0 -> "In ${hours}h ${minutes}min"
                        minutes > 0 -> "In ${minutes}min"
                        else -> "Now"
                    }
                }
            }
            date.isEqual(today) -> "Today"
            date.isEqual(today.plusDays(1)) && timeStr.isNotEmpty() -> "Tomorrow at $timeStr"
            date.isEqual(today.plusDays(1)) -> "Tomorrow"
            else -> DateTimeUtils.formatDate(scheduledDate) + if (timeStr.isNotEmpty()) " • $timeStr" else ""
        }
    } catch (_: Exception) {
        null
    }
}

@Composable
private fun ServiceChip(serviceName: String) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.secondaryContainer)
            .padding(horizontal = 8.dp, vertical = 4.dp)
    ) {
        Text(
            text = serviceName,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSecondaryContainer
        )
    }
}
