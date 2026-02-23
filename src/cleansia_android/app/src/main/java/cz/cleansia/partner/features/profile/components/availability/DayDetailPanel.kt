package cz.cleansia.partner.features.profile.components.availability

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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.EventBusy
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.core.utils.DateTimeUtils
import cz.cleansia.partner.domain.models.profile.DateOverride
import cz.cleansia.partner.domain.models.profile.ResolvedDaySchedule
import cz.cleansia.partner.domain.models.profile.TimeSlot
import cz.cleansia.partner.ui.theme.CleansiaColors
import java.time.format.DateTimeFormatter
import java.time.format.TextStyle
import java.util.Locale

@Composable
fun DayDetailPanel(
    schedule: ResolvedDaySchedule,
    isEditing: Boolean,
    onEditHours: (List<TimeSlot>) -> Unit,
    onMarkException: (DateOverride) -> Unit,
    onRemoveException: () -> Unit,
    modifier: Modifier = Modifier
) {
    var showEditTimeSlots by remember { mutableStateOf(false) }
    var showExceptionDialog by remember { mutableStateOf(false) }

    val dayOfWeekName = schedule.date.dayOfWeek.getDisplayName(TextStyle.FULL, Locale.getDefault())
    val dateFormatted = schedule.date.format(DateTimeFormatter.ofPattern("MMMM d, yyyy"))

    Column(
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f))
            .padding(16.dp)
    ) {
        Text(
            text = "$dayOfWeekName, $dateFormatted",
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onSurface
        )

        Spacer(modifier = Modifier.height(8.dp))

        val (badgeText, badgeColor, badgeBgColor) = when {
            schedule.isOverride && !schedule.isWorkingDay -> Triple(
                stringResource(R.string.exception_day_off),
                MaterialTheme.colorScheme.onErrorContainer,
                MaterialTheme.colorScheme.errorContainer
            )
            schedule.isOverride && schedule.isWorkingDay -> Triple(
                stringResource(R.string.custom_hours),
                Color(0xFF92400E),
                Color(0xFFFEF3C7)
            )
            schedule.isWorkingDay -> Triple(
                stringResource(R.string.working_day),
                CleansiaColors.onSuccessContainer,
                CleansiaColors.successContainer
            )
            else -> Triple(
                stringResource(R.string.day_off),
                MaterialTheme.colorScheme.onSurfaceVariant,
                MaterialTheme.colorScheme.surfaceVariant
            )
        }

        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(16.dp))
                .background(badgeBgColor)
                .padding(horizontal = 12.dp, vertical = 4.dp)
        ) {
            Text(
                text = badgeText,
                style = MaterialTheme.typography.labelSmall,
                fontWeight = FontWeight.Medium,
                color = badgeColor
            )
        }

        if (schedule.isWorkingDay && schedule.timeSlots.isNotEmpty()) {
            Spacer(modifier = Modifier.height(12.dp))
            schedule.timeSlots.forEach { slot ->
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.AccessTime,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(16.dp)
                    )
                    Text(
                        text = "${DateTimeUtils.formatTimeOnly(slot.startTime)} - ${DateTimeUtils.formatTimeOnly(slot.endTime)}",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
            }
        }

        if (!schedule.overrideNote.isNullOrBlank()) {
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = schedule.overrideNote,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }

        if (isEditing) {
            Spacer(modifier = Modifier.height(12.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                if (schedule.isWorkingDay) {
                    TextButton(
                        onClick = { showEditTimeSlots = true }
                    ) {
                        Icon(
                            imageVector = Icons.Default.Edit,
                            contentDescription = null,
                            modifier = Modifier.size(16.dp)
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(stringResource(R.string.edit_hours))
                    }
                }

                if (schedule.isOverride) {
                    TextButton(
                        onClick = onRemoveException
                    ) {
                        Icon(
                            imageVector = Icons.Default.Delete,
                            contentDescription = null,
                            modifier = Modifier.size(16.dp),
                            tint = MaterialTheme.colorScheme.error
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(
                            text = stringResource(R.string.remove_exception),
                            color = MaterialTheme.colorScheme.error
                        )
                    }
                } else {
                    TextButton(
                        onClick = { showExceptionDialog = true }
                    ) {
                        Icon(
                            imageVector = Icons.Default.EventBusy,
                            contentDescription = null,
                            modifier = Modifier.size(16.dp)
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(stringResource(R.string.mark_as_exception))
                    }
                }
            }
        }
    }

    if (showEditTimeSlots) {
        EditTimeSlotsDialog(
            currentSlots = schedule.timeSlots,
            onDismiss = { showEditTimeSlots = false },
            onConfirm = { slots ->
                onEditHours(slots)
                showEditTimeSlots = false
            }
        )
    }

    if (showExceptionDialog) {
        MarkExceptionDialog(
            date = schedule.date,
            onDismiss = { showExceptionDialog = false },
            onConfirm = { override ->
                onMarkException(override)
                showExceptionDialog = false
            }
        )
    }
}
