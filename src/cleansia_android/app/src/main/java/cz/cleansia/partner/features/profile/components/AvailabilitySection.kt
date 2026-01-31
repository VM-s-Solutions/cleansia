package cz.cleansia.partner.features.profile.components

import androidx.compose.animation.animateColorAsState
import androidx.compose.foundation.background
import androidx.compose.foundation.border
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
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material3.IconButton
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TimePicker
import androidx.compose.material3.TimePickerState
import androidx.compose.material3.rememberTimePickerState
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
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.TimeSlot
import cz.cleansia.partner.ui.theme.CleansiaColors
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
 * Availability section for viewing weekly schedule
 */
@Composable
fun AvailabilityViewSection(
    availability: List<DayAvailability>,
    modifier: Modifier = Modifier
) {
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
                    text = stringResource(R.string.availability),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }

            Spacer(modifier = Modifier.height(16.dp))

            // Week days overview
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                DayOfWeek.entries.forEach { day ->
                    val dayAvailability = availability.find { it.dayOfWeek == day.calendarDay }
                    DayIndicator(
                        day = day,
                        isAvailable = dayAvailability?.isAvailable == true
                    )
                }
            }

            // Show detailed schedule if any day has times
            val hasSlots = availability.any { it.isAvailable && it.effectiveTimeSlots().isNotEmpty() }
            if (hasSlots) {
                Spacer(modifier = Modifier.height(16.dp))
                Column(
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    availability
                        .filter { it.isAvailable }
                        .forEach { dayAvail ->
                            val day = DayOfWeek.entries.find { it.calendarDay == dayAvail.dayOfWeek }
                            if (day != null) {
                                val slots = dayAvail.effectiveTimeSlots()
                                Column(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .clip(RoundedCornerShape(8.dp))
                                        .background(MaterialTheme.colorScheme.surfaceVariant)
                                        .padding(12.dp),
                                    verticalArrangement = Arrangement.spacedBy(4.dp)
                                ) {
                                    Text(
                                        text = day.displayName,
                                        style = MaterialTheme.typography.bodyMedium,
                                        fontWeight = FontWeight.Medium,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                    slots.forEach { slot ->
                                        Text(
                                            text = "${slot.startTime} - ${slot.endTime}",
                                            style = MaterialTheme.typography.bodyMedium,
                                            color = MaterialTheme.colorScheme.primary
                                        )
                                    }
                                }
                            }
                        }
                }
            }

            // Empty state
            if (availability.isEmpty() || availability.none { it.isAvailable }) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 16.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = stringResource(R.string.no_availability_set),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}

@Composable
private fun DayIndicator(
    day: DayOfWeek,
    isAvailable: Boolean
) {
    val backgroundColor by animateColorAsState(
        targetValue = if (isAvailable) CleansiaColors.successContainer else MaterialTheme.colorScheme.surfaceVariant,
        label = "dayBgColor"
    )
    val textColor by animateColorAsState(
        targetValue = if (isAvailable) CleansiaColors.onSuccessContainer else MaterialTheme.colorScheme.onSurfaceVariant,
        label = "dayTextColor"
    )

    Column(
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Box(
            modifier = Modifier
                .size(40.dp)
                .clip(CircleShape)
                .background(backgroundColor),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = day.shortName.take(1),
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.Bold,
                color = textColor
            )
        }
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            text = day.shortName,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
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

@Composable
private fun TimeChip(
    time: String,
    onClick: () -> Unit
) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(4.dp))
            .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.1f))
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.primary.copy(alpha = 0.3f),
                shape = RoundedCornerShape(4.dp)
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 8.dp, vertical = 4.dp),
        contentAlignment = Alignment.Center
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            Icon(
                imageVector = Icons.Default.AccessTime,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(14.dp)
            )
            Text(
                text = time,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.primary
            )
        }
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
        initialMinute = initialMinute
    )

    Dialog(onDismissRequest = onDismiss) {
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text(
                    text = stringResource(R.string.select_time),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold
                )

                Spacer(modifier = Modifier.height(16.dp))

                TimePicker(state = timePickerState)

                Spacer(modifier = Modifier.height(16.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.End
                ) {
                    TextButton(onClick = onDismiss) {
                        Text(stringResource(R.string.cancel))
                    }
                    TextButton(
                        onClick = {
                            onConfirm(timePickerState.hour, timePickerState.minute)
                        }
                    ) {
                        Text(stringResource(R.string.confirm))
                    }
                }
            }
        }
    }
}

private fun parseHour(time: String?): Int {
    return time?.split(":")?.getOrNull(0)?.toIntOrNull() ?: 9
}

private fun parseMinute(time: String?): Int {
    return time?.split(":")?.getOrNull(1)?.toIntOrNull() ?: 0
}
