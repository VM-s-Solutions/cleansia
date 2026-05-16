package cz.cleansia.partner.features.profile.components.availability

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
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TimePicker
import androidx.compose.material3.rememberModalBottomSheetState
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
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.TimeSlot
import cz.cleansia.partner.features.profile.components.DayOfWeek
import cz.cleansia.partner.ui.components.TimeChip
import java.util.Calendar

/**
 * Quick setup presets for common schedules
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun QuickSetupPresets(
    onPresetSelected: (List<DayAvailability>) -> Unit,
    modifier: Modifier = Modifier
) {
    // Which preset dialog to show: null = none, 0 = Mon-Fri, 1 = Mon-Sat, 2 = Custom
    var activePreset by remember { mutableStateOf<Int?>(null) }

    val monFriDays = setOf(
        Calendar.MONDAY, Calendar.TUESDAY, Calendar.WEDNESDAY,
        Calendar.THURSDAY, Calendar.FRIDAY
    )
    val monSatDays = setOf(
        Calendar.MONDAY, Calendar.TUESDAY, Calendar.WEDNESDAY,
        Calendar.THURSDAY, Calendar.FRIDAY, Calendar.SATURDAY
    )

    Column(modifier = modifier.fillMaxWidth()) {
        Text(
            text = stringResource(R.string.quick_setup),
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onSurface
        )

        Spacer(modifier = Modifier.height(8.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            PresetChip(
                label = stringResource(R.string.mon_fri_9_5),
                onClick = { activePreset = 0 },
                modifier = Modifier.weight(1f)
            )
            PresetChip(
                label = stringResource(R.string.mon_sat_8_4),
                onClick = { activePreset = 1 },
                modifier = Modifier.weight(1f)
            )
            PresetChip(
                label = stringResource(R.string.custom),
                onClick = { activePreset = 2 },
                modifier = Modifier.weight(1f)
            )
        }
    }

    activePreset?.let { preset ->
        val defaultDays = when (preset) {
            0 -> monFriDays
            1 -> monSatDays
            else -> monFriDays
        }
        val defaultStart = when (preset) {
            0 -> "09:00"
            1 -> "08:00"
            else -> "09:00"
        }
        val defaultEnd = when (preset) {
            0 -> "17:00"
            1 -> "16:00"
            else -> "17:00"
        }
        CustomPresetDialog(
            initialDays = defaultDays,
            initialStartTime = defaultStart,
            initialEndTime = defaultEnd,
            onDismiss = { activePreset = null },
            onConfirm = { selectedDays, startTime, endTime ->
                onPresetSelected(createPresetSchedule(selectedDays, startTime, endTime))
                activePreset = null
            }
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CustomPresetDialog(
    initialDays: Set<Int> = setOf(
        Calendar.MONDAY, Calendar.TUESDAY, Calendar.WEDNESDAY,
        Calendar.THURSDAY, Calendar.FRIDAY
    ),
    initialStartTime: String = "09:00",
    initialEndTime: String = "17:00",
    onDismiss: () -> Unit,
    onConfirm: (selectedDays: List<Int>, startTime: String, endTime: String) -> Unit
) {
    var startTime by remember { mutableStateOf(initialStartTime) }
    var endTime by remember { mutableStateOf(initialEndTime) }
    var selectedDays by remember { mutableStateOf(initialDays) }
    var showTimePicker by remember { mutableStateOf(false) }
    var editingStartTime by remember { mutableStateOf(true) }
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = sheetState,
        shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp),
        containerColor = MaterialTheme.colorScheme.surface
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .navigationBarsPadding()
                .padding(horizontal = 24.dp)
                .padding(bottom = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // Schedule icon in colored circle
            Box(
                modifier = Modifier
                    .size(56.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primaryContainer),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Default.Schedule,
                    contentDescription = null,
                    modifier = Modifier.size(28.dp),
                    tint = MaterialTheme.colorScheme.onPrimaryContainer
                )
            }

            Spacer(modifier = Modifier.height(16.dp))

            Text(
                text = stringResource(R.string.schedule_setup_title),
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(8.dp))

            Text(
                text = stringResource(R.string.schedule_setup_sheet_desc),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )

            Spacer(modifier = Modifier.height(20.dp))

            // Working hours
            Text(
                text = stringResource(R.string.working_hours),
                style = MaterialTheme.typography.titleSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.fillMaxWidth()
            )

            Spacer(modifier = Modifier.height(8.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                TimeChip(
                    time = startTime,
                    onClick = {
                        editingStartTime = true
                        showTimePicker = true
                    }
                )
                Text(
                    text = "-",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                TimeChip(
                    time = endTime,
                    onClick = {
                        editingStartTime = false
                        showTimePicker = true
                    }
                )
            }

            Spacer(modifier = Modifier.height(20.dp))

            // Working days
            Text(
                text = stringResource(R.string.working_days),
                style = MaterialTheme.typography.titleSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.fillMaxWidth()
            )

            Spacer(modifier = Modifier.height(8.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                DayOfWeek.entries.forEach { day ->
                    val isActive = day.calendarDay in selectedDays
                    Box(
                        modifier = Modifier
                            .size(40.dp)
                            .clip(CircleShape)
                            .background(
                                if (isActive) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.surfaceVariant
                            )
                            .clickable {
                                selectedDays = if (isActive) {
                                    selectedDays - day.calendarDay
                                } else {
                                    selectedDays + day.calendarDay
                                }
                            },
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = day.shortName.take(2),
                            style = MaterialTheme.typography.labelSmall,
                            fontWeight = FontWeight.Bold,
                            color = if (isActive) MaterialTheme.colorScheme.onPrimary
                            else MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(24.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                TextButton(
                    onClick = onDismiss,
                    modifier = Modifier.weight(1f)
                ) {
                    Text(stringResource(R.string.cancel))
                }
                Button(
                    onClick = {
                        onConfirm(selectedDays.toList(), startTime, endTime)
                    },
                    enabled = selectedDays.isNotEmpty(),
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Text(stringResource(R.string.apply))
                }
            }
        }
    }

    if (showTimePicker) {
        val time = if (editingStartTime) startTime else endTime
        val parts = time.split(":")
        val hour = parts.getOrNull(0)?.toIntOrNull() ?: 9
        val minute = parts.getOrNull(1)?.toIntOrNull() ?: 0

        val timePickerState = rememberTimePickerState(
            initialHour = hour,
            initialMinute = minute
        )

        Dialog(onDismissRequest = { showTimePicker = false }) {
            androidx.compose.material3.Card(
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
                        TextButton(onClick = { showTimePicker = false }) {
                            Text(stringResource(R.string.cancel))
                        }
                        TextButton(onClick = {
                            val newTime = String.format("%02d:%02d", timePickerState.hour, timePickerState.minute)
                            if (editingStartTime) startTime = newTime else endTime = newTime
                            showTimePicker = false
                        }) {
                            Text(stringResource(R.string.confirm))
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun PresetChip(
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(8.dp))
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.primary.copy(alpha = 0.5f),
                shape = RoundedCornerShape(8.dp)
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 10.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.primary,
            textAlign = TextAlign.Center,
            maxLines = 1
        )
    }
}

internal fun createPresetSchedule(
    activeDays: List<Int>,
    startTime: String,
    endTime: String
): List<DayAvailability> {
    return DayOfWeek.entries.map { day ->
        DayAvailability(
            dayOfWeek = day.calendarDay,
            isAvailable = day.calendarDay in activeDays,
            timeSlots = if (day.calendarDay in activeDays) listOf(TimeSlot(startTime, endTime)) else listOf(TimeSlot())
        )
    }
}
