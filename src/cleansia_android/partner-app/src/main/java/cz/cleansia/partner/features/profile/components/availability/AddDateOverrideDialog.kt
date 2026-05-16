package cz.cleansia.partner.features.profile.components.availability

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
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.Card
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import cz.cleansia.partner.R
import cz.cleansia.partner.core.utils.DateTimeUtils
import cz.cleansia.partner.domain.models.profile.DateOverride
import cz.cleansia.partner.domain.models.profile.TimeSlot
import cz.cleansia.partner.ui.components.TimeChip
import cz.cleansia.partner.ui.components.TimePickerDialog
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private enum class TimeType { START, END }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun AddDateOverrideDialog(
    existingDates: Set<String>,
    onDismiss: () -> Unit,
    onConfirm: (DateOverride) -> Unit
) {
    var selectedDate by remember { mutableStateOf<String?>(null) }
    var isAvailable by remember { mutableStateOf(false) }
    var timeSlots by remember { mutableStateOf(listOf(TimeSlot("09:00", "17:00"))) }
    var note by remember { mutableStateOf("") }
    var showDatePicker by remember { mutableStateOf(true) }
    var showTimePicker by remember { mutableStateOf(false) }
    var editingSlotIndex by remember { mutableStateOf(0) }
    var editingTimeType by remember { mutableStateOf(TimeType.START) }

    val datePickerState = rememberDatePickerState()

    if (showDatePicker && selectedDate == null) {
        DatePickerDialog(
            onDismissRequest = onDismiss,
            confirmButton = {
                TextButton(
                    onClick = {
                        datePickerState.selectedDateMillis?.let { millis ->
                            val dateFormat = SimpleDateFormat("yyyy-MM-dd", Locale.getDefault())
                            val dateStr = dateFormat.format(Date(millis))
                            if (dateStr !in existingDates) {
                                selectedDate = dateStr
                                showDatePicker = false
                            }
                        }
                    }
                ) {
                    Text(stringResource(R.string.confirm))
                }
            },
            dismissButton = {
                TextButton(onClick = onDismiss) {
                    Text(stringResource(R.string.cancel))
                }
            }
        ) {
            DatePicker(state = datePickerState)
        }
        return
    }

    if (selectedDate != null) {
        Dialog(onDismissRequest = onDismiss) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(16.dp)
            ) {
                Column(
                    modifier = Modifier.padding(16.dp)
                ) {
                    Text(
                        text = stringResource(R.string.add_date_override),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold
                    )

                    Spacer(modifier = Modifier.height(12.dp))

                    // Selected date display
                    Row(
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(
                            imageVector = Icons.Default.CalendarToday,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.size(18.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = DateTimeUtils.formatDate(selectedDate!!),
                            style = MaterialTheme.typography.bodyLarge,
                            fontWeight = FontWeight.Medium
                        )
                    }

                    Spacer(modifier = Modifier.height(16.dp))

                    // Available toggle
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = stringResource(R.string.set_custom_hours),
                            style = MaterialTheme.typography.bodyMedium
                        )
                        Switch(
                            checked = isAvailable,
                            onCheckedChange = { isAvailable = it },
                            colors = SwitchDefaults.colors(
                                checkedThumbColor = MaterialTheme.colorScheme.primary,
                                checkedTrackColor = MaterialTheme.colorScheme.primaryContainer
                            )
                        )
                    }

                    if (!isAvailable) {
                        Text(
                            text = stringResource(R.string.mark_unavailable),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.error
                        )
                    }

                    // Time slots (only if available)
                    if (isAvailable) {
                        Spacer(modifier = Modifier.height(12.dp))
                        timeSlots.forEachIndexed { index, slot ->
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(8.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                TimeChip(
                                    time = slot.startTime,
                                    onClick = {
                                        editingSlotIndex = index
                                        editingTimeType = TimeType.START
                                        showTimePicker = true
                                    }
                                )
                                Text("-")
                                TimeChip(
                                    time = slot.endTime,
                                    onClick = {
                                        editingSlotIndex = index
                                        editingTimeType = TimeType.END
                                        showTimePicker = true
                                    }
                                )
                                if (timeSlots.size > 1) {
                                    IconButton(
                                        onClick = {
                                            timeSlots = timeSlots.toMutableList().also { it.removeAt(index) }
                                        },
                                        modifier = Modifier.size(24.dp)
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.Close,
                                            contentDescription = "Remove",
                                            tint = MaterialTheme.colorScheme.error,
                                            modifier = Modifier.size(16.dp)
                                        )
                                    }
                                }
                            }
                            if (index < timeSlots.size - 1) {
                                Spacer(modifier = Modifier.height(4.dp))
                            }
                        }
                        Spacer(modifier = Modifier.height(4.dp))
                        TextButton(
                            onClick = {
                                timeSlots = timeSlots + TimeSlot("09:00", "17:00")
                            }
                        ) {
                            Icon(
                                imageVector = Icons.Default.Add,
                                contentDescription = null,
                                modifier = Modifier.size(16.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("Add slot")
                        }
                    }

                    Spacer(modifier = Modifier.height(12.dp))

                    // Note
                    OutlinedTextField(
                        value = note,
                        onValueChange = { note = it },
                        label = { Text(stringResource(R.string.date_override_note)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true
                    )

                    Spacer(modifier = Modifier.height(16.dp))

                    // Buttons
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.End
                    ) {
                        TextButton(onClick = onDismiss) {
                            Text(stringResource(R.string.cancel))
                        }
                        TextButton(
                            onClick = {
                                onConfirm(
                                    DateOverride(
                                        date = selectedDate!!,
                                        isAvailable = isAvailable,
                                        timeSlots = if (isAvailable) timeSlots else emptyList(),
                                        note = note.ifBlank { null }
                                    )
                                )
                            }
                        ) {
                            Text(stringResource(R.string.confirm))
                        }
                    }
                }
            }
        }
    }

    // Time picker for override slots
    if (showTimePicker) {
        val currentSlot = timeSlots.getOrElse(editingSlotIndex) { TimeSlot() }
        val currentTime = if (editingTimeType == TimeType.START) currentSlot.startTime else currentSlot.endTime

        TimePickerDialog(
            initialHour = parseHour(currentTime),
            initialMinute = parseMinute(currentTime),
            onDismiss = { showTimePicker = false },
            onConfirm = { hour, minute ->
                val timeString = String.format("%02d:%02d", hour, minute)
                timeSlots = timeSlots.toMutableList().also { slots ->
                    val slot = slots.getOrElse(editingSlotIndex) { TimeSlot() }
                    slots[editingSlotIndex] = if (editingTimeType == TimeType.START) {
                        slot.copy(startTime = timeString)
                    } else {
                        slot.copy(endTime = timeString)
                    }
                }
                showTimePicker = false
            }
        )
    }
}

internal fun parseHour(time: String?): Int {
    return time?.split(":")?.getOrNull(0)?.toIntOrNull() ?: 9
}

internal fun parseMinute(time: String?): Int {
    return time?.split(":")?.getOrNull(1)?.toIntOrNull() ?: 0
}
