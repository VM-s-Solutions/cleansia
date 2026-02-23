package cz.cleansia.partner.features.profile.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
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
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TimePicker
import androidx.compose.material3.rememberTimePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import cz.cleansia.partner.R
import cz.cleansia.partner.features.profile.components.availability.QuickSetupPresets
import cz.cleansia.partner.domain.models.profile.DateOverride
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.TimeSlot
import java.time.LocalDate
import java.time.YearMonth
import java.time.format.DateTimeFormatter
import java.util.Calendar

/**
 * Unified availability section combining calendar view with weekly schedule editing
 * and date exception management. Replaces separate AvailabilityViewSection and DateOverridesViewSection.
 */
@Composable
fun UnifiedAvailabilitySection(
    availability: List<DayAvailability>,
    dateOverrides: List<DateOverride>,
    isEditing: Boolean,
    isSaving: Boolean,
    onEditToggle: () -> Unit,
    onSaveAvailability: () -> Unit,
    onAvailabilityChange: (List<DayAvailability>) -> Unit,
    onAddDateOverride: (DateOverride) -> Unit,
    onRemoveDateOverride: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    var currentMonth by remember { mutableStateOf(YearMonth.now()) }
    var selectedDate by remember { mutableStateOf(LocalDate.now()) }

    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            // Header with edit/save buttons
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.CalendarMonth,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(20.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.availability),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                }

                if (isEditing) {
                    Row {
                        IconButton(
                            onClick = onEditToggle,
                            modifier = Modifier.size(32.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Close,
                                contentDescription = stringResource(R.string.cancel),
                                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.size(20.dp)
                            )
                        }
                        Spacer(modifier = Modifier.width(4.dp))
                        IconButton(
                            onClick = onSaveAvailability,
                            enabled = !isSaving,
                            modifier = Modifier.size(32.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Save,
                                contentDescription = stringResource(R.string.save),
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(20.dp)
                            )
                        }
                    }
                } else {
                    IconButton(
                        onClick = onEditToggle,
                        modifier = Modifier.size(32.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Edit,
                            contentDescription = stringResource(R.string.edit),
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.size(20.dp)
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Quick setup presets (only in edit mode)
            if (isEditing) {
                QuickSetupPresets(
                    onPresetSelected = { preset -> onAvailabilityChange(preset) }
                )
                Spacer(modifier = Modifier.height(16.dp))
            }

            // Day toggles for weekly schedule (only in edit mode)
            if (isEditing) {
                WeeklyScheduleEditor(
                    availability = availability,
                    onAvailabilityChange = onAvailabilityChange
                )
                Spacer(modifier = Modifier.height(16.dp))
            }

            // Calendar view
            AvailabilityCalendarView(
                availability = availability,
                dateOverrides = dateOverrides,
                selectedDate = selectedDate,
                currentMonth = currentMonth,
                isEditing = isEditing,
                onDateSelected = { date ->
                    selectedDate = date
                    // Ensure the month view follows the selected date
                    if (YearMonth.from(date) != currentMonth) {
                        currentMonth = YearMonth.from(date)
                    }
                },
                onMonthChanged = { month ->
                    currentMonth = month
                },
                onEditHours = { date, slots ->
                    // Editing hours for a specific date creates/updates an override
                    val dateStr = date.format(DateTimeFormatter.ofPattern("yyyy-MM-dd"))
                    onAddDateOverride(
                        DateOverride(
                            date = dateStr,
                            isAvailable = true,
                            timeSlots = slots
                        )
                    )
                },
                onMarkException = { override ->
                    onAddDateOverride(override)
                },
                onRemoveException = { dateStr ->
                    onRemoveDateOverride(dateStr)
                }
            )
        }
    }
}

/**
 * Weekly schedule editor with day toggles and per-day time editing.
 * Each active day shows its start/end times that can be individually adjusted.
 */
@Composable
private fun WeeklyScheduleEditor(
    availability: List<DayAvailability>,
    onAvailabilityChange: (List<DayAvailability>) -> Unit
) {
    // Track which day is being time-edited: Pair(calendarDay, isStartTime)
    var editingTime by remember { mutableStateOf<Pair<Int, Boolean>?>(null) }

    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(
            text = stringResource(R.string.weekly_schedule),
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onSurface
        )
        Spacer(modifier = Modifier.height(4.dp))

        // Compact day toggles row
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceEvenly
        ) {
            DayOfWeek.entries.forEach { day ->
                val dayAvail = availability.find { it.dayOfWeek == day.calendarDay }
                val isAvailable = dayAvail?.isAvailable == true

                DayToggleChip(
                    label = day.shortName.take(2),
                    isActive = isAvailable,
                    onClick = {
                        val newList = availability.toMutableList()
                        val index = newList.indexOfFirst { it.dayOfWeek == day.calendarDay }
                        if (index >= 0) {
                            newList[index] = newList[index].copy(isAvailable = !isAvailable)
                        } else {
                            newList.add(
                                DayAvailability(
                                    dayOfWeek = day.calendarDay,
                                    isAvailable = true,
                                    timeSlots = listOf(TimeSlot("09:00", "17:00"))
                                )
                            )
                        }
                        onAvailabilityChange(newList)
                    }
                )
            }
        }

        // Per-day time slots for active days
        val activeDays = DayOfWeek.entries
            .mapNotNull { day ->
                availability.find { it.dayOfWeek == day.calendarDay && it.isAvailable }
                    ?.let { day to it }
            }

        if (activeDays.isNotEmpty()) {
            Spacer(modifier = Modifier.height(8.dp))
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                activeDays.forEach { (day, dayAvail) ->
                    val slot = dayAvail.timeSlots.firstOrNull() ?: TimeSlot("09:00", "17:00")
                    DayTimeRow(
                        dayLabel = day.shortName,
                        startTime = slot.startTime,
                        endTime = slot.endTime,
                        onStartTimeClick = { editingTime = Pair(day.calendarDay, true) },
                        onEndTimeClick = { editingTime = Pair(day.calendarDay, false) }
                    )
                }
            }
        }
    }

    // Time picker dialog
    editingTime?.let { (calendarDay, isStart) ->
        val dayAvail = availability.find { it.dayOfWeek == calendarDay }
        val slot = dayAvail?.timeSlots?.firstOrNull() ?: TimeSlot("09:00", "17:00")
        val currentTime = if (isStart) slot.startTime else slot.endTime
        val hour = currentTime.substringBefore(":").toIntOrNull() ?: 9
        val minute = currentTime.substringAfter(":").toIntOrNull() ?: 0

        TimePickerDialog(
            initialHour = hour,
            initialMinute = minute,
            onDismiss = { editingTime = null },
            onConfirm = { h, m ->
                val newTime = String.format("%02d:%02d", h, m)
                val newList = availability.toMutableList()
                val index = newList.indexOfFirst { it.dayOfWeek == calendarDay }
                if (index >= 0) {
                    val oldSlot = newList[index].timeSlots.firstOrNull() ?: TimeSlot("09:00", "17:00")
                    val updatedSlot = if (isStart) oldSlot.copy(startTime = newTime) else oldSlot.copy(endTime = newTime)
                    newList[index] = newList[index].copy(timeSlots = listOf(updatedSlot))
                }
                onAvailabilityChange(newList)
                editingTime = null
            }
        )
    }
}

@Composable
private fun DayTimeRow(
    dayLabel: String,
    startTime: String,
    endTime: String,
    onStartTimeClick: () -> Unit,
    onEndTimeClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f))
            .padding(horizontal = 12.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(
            text = dayLabel,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.width(40.dp)
        )

        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            TimeChip(time = startTime, onClick = onStartTimeClick)
            Text(
                text = "–",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            TimeChip(time = endTime, onClick = onEndTimeClick)
        }
    }
}

@Composable
private fun TimeChip(
    time: String,
    onClick: () -> Unit
) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(6.dp))
            .background(MaterialTheme.colorScheme.primaryContainer)
            .clickable(onClick = onClick)
            .padding(horizontal = 10.dp, vertical = 4.dp)
    ) {
        Text(
            text = time,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onPrimaryContainer
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun TimePickerDialog(
    initialHour: Int,
    initialMinute: Int,
    onDismiss: () -> Unit,
    onConfirm: (hour: Int, minute: Int) -> Unit
) {
    val timePickerState = rememberTimePickerState(
        initialHour = initialHour,
        initialMinute = initialMinute,
        is24Hour = true
    )

    Dialog(onDismissRequest = onDismiss) {
        Card(
            shape = RoundedCornerShape(24.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                TimePicker(state = timePickerState)
                Spacer(modifier = Modifier.height(8.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.End
                ) {
                    TextButton(onClick = onDismiss) {
                        Text(stringResource(R.string.cancel))
                    }
                    TextButton(onClick = {
                        onConfirm(timePickerState.hour, timePickerState.minute)
                    }) {
                        Text(stringResource(R.string.confirm))
                    }
                }
            }
        }
    }
}

@Composable
private fun DayToggleChip(
    label: String,
    isActive: Boolean,
    onClick: () -> Unit
) {
    val bgColor = if (isActive) {
        MaterialTheme.colorScheme.primary
    } else {
        MaterialTheme.colorScheme.surfaceVariant
    }
    val textColor = if (isActive) {
        MaterialTheme.colorScheme.onPrimary
    } else {
        MaterialTheme.colorScheme.onSurfaceVariant
    }

    Box(
        modifier = Modifier
            .size(40.dp)
            .clip(CircleShape)
            .background(bgColor)
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            fontWeight = FontWeight.Bold,
            color = textColor
        )
    }
}
