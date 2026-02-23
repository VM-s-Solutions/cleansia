package cz.cleansia.partner.features.profile.components

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
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.EventBusy
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
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
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.DateOverride
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.TimeSlot
import cz.cleansia.partner.features.profile.components.availability.AddDateOverrideDialog
import cz.cleansia.partner.features.profile.components.availability.DateOverrideCard
import cz.cleansia.partner.features.profile.components.availability.parseHour
import cz.cleansia.partner.features.profile.components.availability.parseMinute
import cz.cleansia.partner.ui.components.TimeChip
import cz.cleansia.partner.ui.components.TimePickerDialog
import java.util.Calendar

/**
 * Days of the week with their display names
 */
enum class DayOfWeek(val displayName: String, val shortName: String, val calendarDay: Int) {
    MONDAY("Monday", "Mon", Calendar.MONDAY),
    TUESDAY("Tuesday", "Tue", Calendar.TUESDAY),
    WEDNESDAY("Wednesday", "Wed", Calendar.WEDNESDAY),
    THURSDAY("Thursday", "Thu", Calendar.THURSDAY),
    FRIDAY("Friday", "Fri", Calendar.FRIDAY),
    SATURDAY("Saturday", "Sat", Calendar.SATURDAY),
    SUNDAY("Sunday", "Sun", Calendar.SUNDAY);

    companion object {
        fun fromCalendarDay(day: Int): DayOfWeek = entries.find { it.calendarDay == day } ?: MONDAY
    }
}

/**
 * Availability editing section with day toggles, multiple time slots, and time pickers
 */
@Composable
fun AvailabilityEditSection(
    availability: List<DayAvailability>,
    onAvailabilityChange: (List<DayAvailability>) -> Unit,
    modifier: Modifier = Modifier
) {
    var showTimePicker by remember { mutableStateOf(false) }
    var editingDay by remember { mutableStateOf<DayOfWeek?>(null) }
    var editingSlotIndex by remember { mutableStateOf(0) }
    var editingTimeType by remember { mutableStateOf<TimeType?>(null) }

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
            // Header
            Row(
                verticalAlignment = Alignment.CenterVertically
            ) {
                Icon(
                    imageVector = Icons.Default.Schedule,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp)
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = stringResource(R.string.set_availability),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }

            Spacer(modifier = Modifier.height(16.dp))

            // Day rows
            DayOfWeek.entries.forEach { day ->
                val dayAvailability = availability.find { it.dayOfWeek == day.calendarDay }
                    ?: DayAvailability(dayOfWeek = day.calendarDay, isAvailable = false)

                DayAvailabilityRow(
                    day = day,
                    availability = dayAvailability,
                    onToggle = { isAvailable ->
                        val newList = availability.toMutableList()
                        val index = newList.indexOfFirst { it.dayOfWeek == day.calendarDay }
                        if (index >= 0) {
                            newList[index] = newList[index].copy(isAvailable = isAvailable)
                        } else {
                            newList.add(DayAvailability(dayOfWeek = day.calendarDay, isAvailable = isAvailable))
                        }
                        onAvailabilityChange(newList)
                    },
                    onSlotStartTimeClick = { slotIndex ->
                        editingDay = day
                        editingSlotIndex = slotIndex
                        editingTimeType = TimeType.START
                        showTimePicker = true
                    },
                    onSlotEndTimeClick = { slotIndex ->
                        editingDay = day
                        editingSlotIndex = slotIndex
                        editingTimeType = TimeType.END
                        showTimePicker = true
                    },
                    onAddSlot = {
                        val newList = availability.toMutableList()
                        val index = newList.indexOfFirst { it.dayOfWeek == day.calendarDay }
                        if (index >= 0) {
                            val currentSlots = newList[index].effectiveTimeSlots().toMutableList()
                            currentSlots.add(TimeSlot("09:00", "17:00"))
                            newList[index] = newList[index].copy(timeSlots = currentSlots)
                        }
                        onAvailabilityChange(newList)
                    },
                    onRemoveSlot = { slotIndex ->
                        val newList = availability.toMutableList()
                        val index = newList.indexOfFirst { it.dayOfWeek == day.calendarDay }
                        if (index >= 0) {
                            val currentSlots = newList[index].effectiveTimeSlots().toMutableList()
                            if (currentSlots.size > 1) {
                                currentSlots.removeAt(slotIndex)
                                newList[index] = newList[index].copy(timeSlots = currentSlots)
                            }
                        }
                        onAvailabilityChange(newList)
                    }
                )

                if (day != DayOfWeek.SUNDAY) {
                    Spacer(modifier = Modifier.height(8.dp))
                }
            }
        }
    }

    // Time picker dialog
    if (showTimePicker && editingDay != null && editingTimeType != null) {
        val dayAvail = availability.find { it.dayOfWeek == editingDay!!.calendarDay }
        val slots = dayAvail?.effectiveTimeSlots() ?: listOf(TimeSlot())
        val currentSlot = slots.getOrElse(editingSlotIndex) { TimeSlot() }
        val currentTime = if (editingTimeType == TimeType.START) currentSlot.startTime else currentSlot.endTime

        TimePickerDialog(
            initialHour = parseHour(currentTime),
            initialMinute = parseMinute(currentTime),
            onDismiss = {
                showTimePicker = false
                editingDay = null
                editingTimeType = null
            },
            onConfirm = { hour, minute ->
                val timeString = String.format("%02d:%02d", hour, minute)
                val newList = availability.toMutableList()
                val index = newList.indexOfFirst { it.dayOfWeek == editingDay!!.calendarDay }

                if (index >= 0) {
                    val currentSlots = newList[index].effectiveTimeSlots().toMutableList()
                    val slot = currentSlots.getOrElse(editingSlotIndex) { TimeSlot() }
                    currentSlots[editingSlotIndex] = if (editingTimeType == TimeType.START) {
                        slot.copy(startTime = timeString)
                    } else {
                        slot.copy(endTime = timeString)
                    }
                    newList[index] = newList[index].copy(timeSlots = currentSlots)
                }

                onAvailabilityChange(newList)
                showTimePicker = false
                editingDay = null
                editingTimeType = null
            }
        )
    }
}

private enum class TimeType { START, END }

@Composable
private fun DayAvailabilityRow(
    day: DayOfWeek,
    availability: DayAvailability,
    onToggle: (Boolean) -> Unit,
    onSlotStartTimeClick: (Int) -> Unit,
    onSlotEndTimeClick: (Int) -> Unit,
    onAddSlot: () -> Unit,
    onRemoveSlot: (Int) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant)
            .padding(12.dp)
    ) {
        // Day name and toggle
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically
            ) {
                Switch(
                    checked = availability.isAvailable,
                    onCheckedChange = onToggle,
                    colors = SwitchDefaults.colors(
                        checkedThumbColor = MaterialTheme.colorScheme.primary,
                        checkedTrackColor = MaterialTheme.colorScheme.primaryContainer
                    )
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = day.shortName,
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Medium,
                    color = if (availability.isAvailable)
                        MaterialTheme.colorScheme.onSurfaceVariant
                    else
                        MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.5f)
                )
            }

            // Add slot button (only if available)
            if (availability.isAvailable) {
                IconButton(
                    onClick = onAddSlot,
                    modifier = Modifier.size(28.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Add,
                        contentDescription = "Add time slot",
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(18.dp)
                    )
                }
            }
        }

        // Time slots (only if available)
        if (availability.isAvailable) {
            val slots = availability.effectiveTimeSlots()
            Spacer(modifier = Modifier.height(8.dp))
            slots.forEachIndexed { slotIndex, slot ->
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(start = 48.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    TimeChip(
                        time = slot.startTime,
                        onClick = { onSlotStartTimeClick(slotIndex) }
                    )
                    Text(
                        text = "-",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    TimeChip(
                        time = slot.endTime,
                        onClick = { onSlotEndTimeClick(slotIndex) }
                    )
                    // Remove button (only if more than 1 slot)
                    if (slots.size > 1) {
                        IconButton(
                            onClick = { onRemoveSlot(slotIndex) },
                            modifier = Modifier.size(24.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Close,
                                contentDescription = "Remove time slot",
                                tint = MaterialTheme.colorScheme.error,
                                modifier = Modifier.size(16.dp)
                            )
                        }
                    }
                }
                if (slotIndex < slots.size - 1) {
                    Spacer(modifier = Modifier.height(4.dp))
                }
            }
        }
    }
}

/**
 * Edit section for managing date-specific schedule exceptions
 */
@Composable
fun DateOverridesEditSection(
    dateOverrides: List<DateOverride>,
    onAddOverride: (DateOverride) -> Unit,
    onRemoveOverride: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    var showAddDialog by remember { mutableStateOf(false) }

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
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.EventBusy,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(20.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.date_overrides),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                }
                IconButton(
                    onClick = { showAddDialog = true },
                    modifier = Modifier.size(32.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Add,
                        contentDescription = stringResource(R.string.add_date_override),
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(20.dp)
                    )
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            if (dateOverrides.isEmpty()) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 12.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = stringResource(R.string.no_date_overrides),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            } else {
                dateOverrides.forEach { override ->
                    DateOverrideCard(
                        override = override,
                        onRemove = { onRemoveOverride(override.date) }
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                }
            }
        }
    }

    if (showAddDialog) {
        AddDateOverrideDialog(
            existingDates = dateOverrides.map { it.date }.toSet(),
            onDismiss = { showAddDialog = false },
            onConfirm = { override ->
                onAddOverride(override)
                showAddDialog = false
            }
        )
    }
}
