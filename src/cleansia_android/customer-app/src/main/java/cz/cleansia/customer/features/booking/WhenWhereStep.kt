package cz.cleansia.customer.features.booking

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.theme.selectionTint
import kotlinx.datetime.Clock
import kotlinx.datetime.DayOfWeek
import kotlinx.datetime.LocalDate
import kotlinx.datetime.LocalDateTime
import kotlinx.datetime.LocalTime
import kotlinx.datetime.TimeZone
import kotlinx.datetime.plus
import kotlinx.datetime.toInstant
import kotlinx.datetime.toLocalDateTime
import kotlinx.datetime.DateTimeUnit

private data class DayChip(
    val label: String,
    val date: String,
    val localDate: LocalDate,
    val available: Boolean = true,
    val isToday: Boolean = false,
)

// Today + next 7 days. Short weekday label, day-of-month number, and the real
// LocalDate so slot selection can build an Instant without parsing. Backend
// has no day-of-week restriction for one-off orders, so every day is bookable;
// per-slot availability is gated only by the lead-time bands in [timeSlotsFor].
private fun buildDays(): List<DayChip> {
    val tz = TimeZone.currentSystemDefault()
    val today = Clock.System.now().toLocalDateTime(tz).date
    return (0..7).map { offset ->
        val d = today.plus(offset, DateTimeUnit.DAY)
        val label = if (offset == 0) {
            "Today"
        } else {
            when (d.dayOfWeek) {
                DayOfWeek.MONDAY -> "Mon"
                DayOfWeek.TUESDAY -> "Tue"
                DayOfWeek.WEDNESDAY -> "Wed"
                DayOfWeek.THURSDAY -> "Thu"
                DayOfWeek.FRIDAY -> "Fri"
                DayOfWeek.SATURDAY -> "Sat"
                DayOfWeek.SUNDAY -> "Sun"
                else -> d.dayOfWeek.name.take(3)
            }
        }
        DayChip(
            label = label,
            date = d.dayOfMonth.toString(),
            localDate = d,
            available = true,
            isToday = offset == 0,
        )
    }
}

private fun combineDateAndTime(date: LocalDate, timeLabel: String): kotlinx.datetime.Instant? {
    // timeLabel is "HH:mm" (08:00, 09:00, ...) from timeSlots below.
    val parts = timeLabel.split(":")
    if (parts.size != 2) return null
    val hour = parts[0].toIntOrNull() ?: return null
    val minute = parts[1].toIntOrNull() ?: return null
    val local = LocalDateTime(date, LocalTime(hour, minute))
    return local.toInstant(TimeZone.currentSystemDefault())
}

private enum class SlotState { Available, Express, Unavailable, Earliest }

private data class TimeSlot(val time: String, val state: SlotState)

// Source-of-truth lead-time bands — keep in sync with backend
// `BookingPolicy` (StandardLeadTimeHours = 4, ExpressLeadTimeHours = 2,
// FirstWindowHour = 8, LastWindowHour = 20). Mobile derives slot states
// dynamically from the user's selected date so "Today" never shows
// already-passed hours as bookable.
private const val STANDARD_LEAD_HOURS = 4
private const val EXPRESS_LEAD_HOURS = 2
private const val FIRST_WINDOW_HOUR = 8
private const val LAST_WINDOW_HOUR = 20

/**
 * Build the 1-hour window list for a given local date, gated on lead time.
 * - Below [EXPRESS_LEAD_HOURS] from now → Unavailable (rendered greyed out).
 * - [EXPRESS_LEAD_HOURS]..[STANDARD_LEAD_HOURS] → Express (+20% surcharge).
 * - First slot ≥ [STANDARD_LEAD_HOURS] → Earliest (visual hint).
 * - The rest → Available.
 *
 * For dates strictly in the future (tomorrow+), every slot is Available
 * (no lead-time checks needed). For "Today", the first selectable slot
 * shifts forward in real time.
 */
private fun timeSlotsFor(date: LocalDate): List<TimeSlot> {
    val tz = TimeZone.currentSystemDefault()
    val now = Clock.System.now()
    val today = now.toLocalDateTime(tz).date
    val isToday = date == today
    var earliestAssigned = false

    return (FIRST_WINDOW_HOUR..LAST_WINDOW_HOUR - 1).map { hour ->
        val label = "%02d:00".format(hour)
        if (!isToday) {
            // Future days: every slot bookable, no express tier needed.
            return@map TimeSlot(label, SlotState.Available)
        }
        val slotInstant = LocalDateTime(date, LocalTime(hour, 0)).toInstant(tz)
        val leadHours = (slotInstant - now).inWholeMinutes / 60.0
        val state = when {
            leadHours < EXPRESS_LEAD_HOURS -> SlotState.Unavailable
            leadHours < STANDARD_LEAD_HOURS -> SlotState.Express
            !earliestAssigned -> { earliestAssigned = true; SlotState.Earliest }
            else -> SlotState.Available
        }
        TimeSlot(label, state)
    }
}

@Composable
fun WhenWhereStep(
    state: BookingState,
    onUpdate: (BookingState) -> Unit,
    onPickAddressOnMap: () -> Unit = {},
) {
    // Rebuilt per composition but cheap — 8 entries, one Clock.now() call.
    val days = androidx.compose.runtime.remember { buildDays() }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(vertical = 16.dp),
    ) {
        // ── WHERE ──
        SectionLabel(stringResource(R.string.booking_where), Modifier.padding(horizontal = 20.dp))
        Spacer(Modifier.height(10.dp))

        // Single "Select address" row — tap to open the Address Manager overlay.
        Column(Modifier.padding(horizontal = 20.dp)) {
            SelectAddressRow(
                street = state.street,
                city = state.city,
                onClick = onPickAddressOnMap,
            )
        }

        Spacer(Modifier.height(28.dp))

        // ── DATE — horizontal day strip ──
        SectionLabel(stringResource(R.string.booking_when), Modifier.padding(horizontal = 20.dp))
        Spacer(Modifier.height(10.dp))

        LazyRow(
            contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 20.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            items(days.size) { idx ->
                val day = days[idx]
                val selected = state.selectedDate == day.label
                DayChipView(day, selected) {
                    if (day.available) {
                        val instant = if (state.selectedTime.isNotBlank()) {
                            combineDateAndTime(day.localDate, state.selectedTime)
                        } else null
                        onUpdate(
                            state.copy(
                                selectedDate = day.label,
                                selectedInstant = instant,
                            ),
                        )
                    }
                }
            }
        }

        Spacer(Modifier.height(24.dp))

        // ── TIME — full-width list, hide unavailable ──
        Row(
            modifier = Modifier.padding(horizontal = 20.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            SectionLabel(stringResource(R.string.booking_select_time))
            Spacer(Modifier.weight(1f))
            Text(
                stringResource(R.string.booking_arrival_window),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Spacer(Modifier.height(10.dp))

        // Slots are derived per-day so "Today" honours real-time lead-time bands.
        val pickedDayChip = days.firstOrNull { it.label == state.selectedDate }
        val daySlots = androidx.compose.runtime.remember(pickedDayChip?.localDate) {
            pickedDayChip?.localDate?.let { timeSlotsFor(it) } ?: emptyList()
        }
        // Hide Unavailable from the visible list — the user can scroll up to find
        // an empty-day note if every slot is gone (uncommon mid-day).
        val visibleSlots = daySlots.filter { it.state != SlotState.Unavailable }

        // Defensive: if the previously-selected time slipped into Unavailable
        // (e.g. user opened the wizard hours ago and the day shifted), clear it
        // so the "Continue" button doesn't carry a now-invalid Instant.
        androidx.compose.runtime.LaunchedEffect(daySlots, state.selectedTime) {
            if (state.selectedTime.isBlank()) return@LaunchedEffect
            val match = daySlots.firstOrNull { it.time == state.selectedTime }
            if (match == null || match.state == SlotState.Unavailable) {
                onUpdate(state.copy(selectedTime = "", selectedInstant = null))
            }
        }

        Column(
            modifier = Modifier.padding(horizontal = 20.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            visibleSlots.forEach { slot ->
                TimeSlotRow(
                    slot = slot,
                    selected = state.selectedTime == slot.time,
                    onClick = {
                        val instant = pickedDayChip?.let { combineDateAndTime(it.localDate, slot.time) }
                        onUpdate(
                            state.copy(
                                selectedTime = slot.time,
                                selectedInstant = instant,
                            ),
                        )
                    },
                )
            }
            if (visibleSlots.isEmpty()) {
                Text(
                    stringResource(R.string.booking_all_slots_booked),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(vertical = 16.dp),
                )
            }
        }

        // Cancellation policy hint under slots
        Spacer(Modifier.height(12.dp))
        Row(
            modifier = Modifier.padding(horizontal = 20.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                Icons.Outlined.Info,
                null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(14.dp),
            )
            Spacer(Modifier.width(6.dp))
            Text(
                stringResource(R.string.booking_cancel_hint),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }

        Spacer(Modifier.height(32.dp))
    }
}

/* ── Select address row — tap to open the Address Manager overlay ── */

@Composable
private fun SelectAddressRow(street: String, city: String, onClick: () -> Unit) {
    val hasSelection = street.isNotBlank()
    val shape = RoundedCornerShape(14.dp)
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(shape)
            .clickable(onClick = onClick)
            .background(if (hasSelection) selectionTint() else MaterialTheme.colorScheme.surface, shape)
            .border(
                width = if (hasSelection) 2.dp else 1.dp,
                color = if (hasSelection) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                shape = shape,
            )
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(40.dp)
                .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.6f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.LocationOn,
                null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            if (hasSelection) {
                Text(
                    street,
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                )
                if (city.isNotBlank()) {
                    Text(
                        city,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                    )
                }
            } else {
                Text(
                    stringResource(R.string.address_manager_select),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Text(
                    stringResource(R.string.booking_select_address_hint),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        Icon(
            Icons.AutoMirrored.Outlined.KeyboardArrowRight,
            null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(20.dp),
        )
    }
}

/* ── Day chip — vertical, day name on top, date number below ── */

@Composable
private fun DayChipView(day: DayChip, selected: Boolean, onClick: () -> Unit) {
    val alpha = if (day.available) 1f else 0.3f
    Column(
        modifier = Modifier
            .width(52.dp)
            .clip(RoundedCornerShape(12.dp))
            .clickable(enabled = day.available, onClick = onClick)
            .background(if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.surface)
            .border(1.dp, if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(12.dp))
            .padding(horizontal = 6.dp, vertical = 10.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(
            day.label,
            style = MaterialTheme.typography.labelMedium,
            color = (if (selected) MaterialTheme.colorScheme.onPrimary else MaterialTheme.colorScheme.onSurfaceVariant).copy(alpha = alpha),
        )
        Text(
            day.date,
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
            color = (if (selected) MaterialTheme.colorScheme.onPrimary else MaterialTheme.colorScheme.onSurface).copy(alpha = alpha),
        )
        // Today dot — reserve space always so all cards have equal height
        Spacer(Modifier.height(2.dp))
        Box(
            Modifier.size(4.dp).background(
                if (day.isToday && !selected) MaterialTheme.colorScheme.primary else androidx.compose.ui.graphics.Color.Transparent,
                CircleShape,
            ),
        )
    }
}

/* ── Time slot row — full-width list row with optional express left stripe ── */

private val ExpressOrange = androidx.compose.ui.graphics.Color(0xFFEA580C)

@Composable
private fun TimeSlotRow(slot: TimeSlot, selected: Boolean, onClick: () -> Unit) {
    val isExpress = slot.state == SlotState.Express
    val isEarliest = slot.state == SlotState.Earliest

    // Selected: surface background + 2dp primary border + primary text.
    // Unselected: surface background + 1dp outlineVariant border.
    val bg = MaterialTheme.colorScheme.surface
    val borderColor = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant
    val borderWidth = if (selected) 2.dp else 1.dp
    val textColor = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurface
    val subTextColor = MaterialTheme.colorScheme.onSurfaceVariant

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .background(bg)
            .border(borderWidth, borderColor, RoundedCornerShape(14.dp)),
    ) {
        // Left accent stripe for express slots (always shown — independent of selection)
        if (isExpress) {
            Box(
                modifier = Modifier
                    .fillMaxHeight()
                    .width(4.dp)
                    .background(ExpressOrange),
            )
        }
        Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            // Leading icon (express = lightning, earliest = clock)
            if (isExpress) {
                Icon(
                    Icons.Outlined.Bolt,
                    null,
                    tint = ExpressOrange,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(Modifier.width(10.dp))
            } else if (isEarliest) {
                Icon(
                    Icons.Outlined.Schedule,
                    null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(Modifier.width(10.dp))
            }
            // Main — time + subtitle tag
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    slot.time,
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = textColor,
                )
                when {
                    isExpress -> Text(
                        stringResource(R.string.booking_slot_express),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
                        color = ExpressOrange,
                    )
                    isEarliest -> Text(
                        stringResource(R.string.booking_slot_earliest),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.primary,
                    )
                }
            }
            // Trailing — checkmark when selected, chevron otherwise
            if (selected) {
                Icon(
                    Icons.Outlined.Check,
                    null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp),
                )
            } else {
                Text(
                    stringResource(R.string.booking_slot_select),
                    style = MaterialTheme.typography.labelSmall,
                    color = subTextColor,
                )
            }
        }
    }
}

@Composable
private fun SectionLabel(text: String, modifier: Modifier = Modifier) {
    Text(text, style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold), color = MaterialTheme.colorScheme.onBackground, modifier = modifier)
}
