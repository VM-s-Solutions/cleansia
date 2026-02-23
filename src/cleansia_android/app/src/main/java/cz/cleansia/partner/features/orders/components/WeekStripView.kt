package cz.cleansia.partner.features.orders.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.time.LocalDate
import java.time.format.TextStyle
import java.util.Locale

@Composable
fun WeekStripView(
    weekStartDate: LocalDate,
    selectedDate: LocalDate,
    ordersPerDay: Map<LocalDate, Int>,
    onDateSelected: (LocalDate) -> Unit,
    onNavigateWeek: (forward: Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    val today = LocalDate.now()
    val days = (0 until 7).map { weekStartDate.plusDays(it.toLong()) }

    Column(modifier = modifier.fillMaxWidth()) {
        // Week navigation header
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = { onNavigateWeek(false) }) {
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.KeyboardArrowLeft,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            Text(
                text = formatWeekLabel(weekStartDate),
                style = MaterialTheme.typography.labelMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            IconButton(onClick = { onNavigateWeek(true) }) {
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }

        // Day cells
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 8.dp),
            horizontalArrangement = Arrangement.SpaceEvenly
        ) {
            days.forEach { date ->
                DayCell(
                    date = date,
                    isSelected = date == selectedDate,
                    isToday = date == today,
                    orderCount = ordersPerDay[date] ?: 0,
                    onClick = { onDateSelected(date) }
                )
            }
        }
    }
}

@Composable
private fun DayCell(
    date: LocalDate,
    isSelected: Boolean,
    isToday: Boolean,
    orderCount: Int,
    onClick: () -> Unit
) {
    val bgColor = when {
        isSelected -> MaterialTheme.colorScheme.primary
        else -> MaterialTheme.colorScheme.surface
    }
    val textColor = when {
        isSelected -> MaterialTheme.colorScheme.onPrimary
        else -> MaterialTheme.colorScheme.onSurface
    }
    val dayNameColor = when {
        isSelected -> MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.8f)
        else -> MaterialTheme.colorScheme.onSurfaceVariant
    }

    val borderModifier = if (isToday && !isSelected) {
        Modifier.border(1.5.dp, MaterialTheme.colorScheme.primary, RoundedCornerShape(12.dp))
    } else {
        Modifier
    }

    Column(
        modifier = Modifier
            .clip(RoundedCornerShape(12.dp))
            .then(borderModifier)
            .background(bgColor, RoundedCornerShape(12.dp))
            .clickable { onClick() }
            .padding(horizontal = 8.dp, vertical = 6.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(2.dp)
    ) {
        Text(
            text = date.dayOfWeek.getDisplayName(TextStyle.SHORT, Locale.getDefault()),
            style = MaterialTheme.typography.labelSmall,
            color = dayNameColor,
            textAlign = TextAlign.Center,
            fontSize = 10.sp
        )
        Text(
            text = date.dayOfMonth.toString(),
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.Bold,
            color = textColor,
            textAlign = TextAlign.Center
        )
        // Dot indicator for orders
        Box(
            modifier = Modifier
                .size(5.dp)
                .background(
                    color = if (orderCount > 0) {
                        if (isSelected) MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.8f)
                        else MaterialTheme.colorScheme.primary
                    } else {
                        MaterialTheme.colorScheme.surface // invisible
                    },
                    shape = CircleShape
                )
        )
    }
}

private fun formatWeekLabel(weekStart: LocalDate): String {
    val weekEnd = weekStart.plusDays(6)
    val startMonth = weekStart.month.getDisplayName(TextStyle.SHORT, Locale.getDefault())
    val endMonth = weekEnd.month.getDisplayName(TextStyle.SHORT, Locale.getDefault())

    return if (weekStart.month == weekEnd.month) {
        "${weekStart.dayOfMonth}–${weekEnd.dayOfMonth} $startMonth ${weekStart.year}"
    } else {
        "${weekStart.dayOfMonth} $startMonth – ${weekEnd.dayOfMonth} $endMonth ${weekStart.year}"
    }
}
