package cz.cleansia.customer.features.booking

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.memberships.ExpressWaiver
import cz.cleansia.customer.core.memberships.ExpressWaiverStatus
import cz.cleansia.customer.ui.theme.selectionTint
import kotlinx.datetime.Clock
import kotlinx.datetime.LocalDate
import kotlinx.datetime.LocalDateTime
import kotlinx.datetime.LocalTime
import kotlinx.datetime.TimeZone
import kotlinx.datetime.plus
import kotlinx.datetime.toInstant
import kotlinx.datetime.toLocalDateTime
import kotlinx.datetime.DateTimeUnit
import kotlinx.datetime.isoDayNumber
import java.time.format.TextStyle
import java.util.Locale
import java.time.DayOfWeek as JavaDayOfWeek

internal data class DayChip(
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
//
// Weekday abbreviations come from CLDR via java.time rather than a hardcoded
// English table, so the strip reads "po/út/st…" in Czech and "пн/вт/ср…" in
// Ukrainian. Only "Today" needs a translated string — pass it in as [todayLabel]
// so this stays a plain function the JVM test suite can call.
internal fun buildDays(locale: Locale, todayLabel: String): List<DayChip> {
    val tz = TimeZone.currentSystemDefault()
    val today = Clock.System.now().toLocalDateTime(tz).date
    return (0..7).map { offset ->
        val d = today.plus(offset, DateTimeUnit.DAY)
        val label = if (offset == 0) {
            todayLabel
        } else {
            // kotlinx.datetime.DayOfWeek is a JVM typealias for java.time.DayOfWeek
            // so .getDisplayName would resolve directly — go through isoDayNumber
            // anyway so the conversion survives a kotlinx-datetime version bump
            // that stops aliasing.
            JavaDayOfWeek.of(d.dayOfWeek.isoDayNumber)
                .getDisplayName(TextStyle.SHORT, locale)
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

internal fun combineDateAndTime(
    date: LocalDate,
    timeLabel: String,
    tz: TimeZone = TimeZone.currentSystemDefault(),
): kotlinx.datetime.Instant? {
    val parts = timeLabel.split(":")
    if (parts.size != 2) return null
    val hour = parts[0].toIntOrNull() ?: return null
    val minute = parts[1].toIntOrNull() ?: return null
    val local = LocalDateTime(date, LocalTime(hour, minute))
    return local.toInstant(tz)
}

internal enum class SlotState { Available, Express, Unavailable }

internal data class TimeSlot(val time: String, val state: SlotState)

// Booking window — keep in sync with backend `BookingPolicy` (FirstWindowHour = 8,
// LastWindowHour = 20). Slot states are derived from the user's selected date so "Today" never shows
// already-passed hours as bookable; the express band itself is [BookingPricing]'s.
private const val EXPRESS_LEAD_HOURS = 2
internal const val FIRST_WINDOW_HOUR = 8
internal const val LAST_WINDOW_HOUR = 20
internal const val BOOKING_SLOT_INTERVAL_MINUTES = 15

/**
 * Build the quarter-hour arrival list for a given local date, gated on lead time.
 * - Below [EXPRESS_LEAD_HOURS] from now → Unavailable (greyed out and disabled in the grid).
 * - Inside [BookingPricing.requiresExpressSurcharge]'s band → Express (a tag, never a price).
 * - The rest → Available.
 *
 * For dates strictly in the future (tomorrow+), every slot is Available
 * (no lead-time checks needed). For "Today", the first selectable slot
 * shifts forward in real time.
 */
internal fun timeSlotsFor(
    date: LocalDate,
    now: kotlinx.datetime.Instant = Clock.System.now(),
    tz: TimeZone = TimeZone.currentSystemDefault(),
): List<TimeSlot> {
    val today = now.toLocalDateTime(tz).date
    val isToday = date == today

    return (FIRST_WINDOW_HOUR * 60 until LAST_WINDOW_HOUR * 60 step BOOKING_SLOT_INTERVAL_MINUTES).map { minutes ->
        val hour = minutes / 60
        val minute = minutes % 60
        val label = "%02d:%02d".format(Locale.ROOT, hour, minute)
        if (!isToday) {
            // Future days: every slot bookable, no express tier needed.
            return@map TimeSlot(label, SlotState.Available)
        }
        val slotInstant = LocalDateTime(date, LocalTime(hour, minute)).toInstant(tz)
        val leadHours = (slotInstant - now).inWholeMinutes / 60.0
        val state = when {
            leadHours < EXPRESS_LEAD_HOURS -> SlotState.Unavailable
            BookingPricing.requiresExpressSurcharge(slotInstant, now) -> SlotState.Express
            else -> SlotState.Available
        }
        TimeSlot(label, state)
    }
}

/**
 * The part of day the time step asks for first, the web wizard's `dayParts`: each holds the arrival
 * times whose hour falls in [fromHour, toHour), sixteen quarter hours apiece on the 15-minute grid.
 * -> /customer-app/ordering-flow#step-2-date-time
 */
internal enum class DayPart(val fromHour: Int, val toHour: Int) {
    Morning(FIRST_WINDOW_HOUR, 12),
    Afternoon(12, 16),
    Evening(16, LAST_WINDOW_HOUR),
    ;

    companion object {
        fun of(time: String): DayPart? {
            val hour = time.substringBefore(':').toIntOrNull() ?: return null
            return entries.firstOrNull { hour >= it.fromHour && hour < it.toHour }
        }
    }
}

/** One part's slots. A part with nothing bookable left (today's morning, by noon) is drawn disabled. */
internal data class DayPartSlots(val part: DayPart, val slots: List<TimeSlot>) {
    val bookableCount: Int get() = slots.count { it.state != SlotState.Unavailable }
    val bookable: Boolean get() = bookableCount > 0
}

internal fun groupByDayPart(slots: List<TimeSlot>): List<DayPartSlots> =
    DayPart.entries.map { part -> DayPartSlots(part, slots.filter { DayPart.of(it.time) == part }) }

/**
 * The part the step opens on: the one that holds the booked time, else the first with a bookable slot.
 * The web always holds a time (09:00, or the date's first bookable slot), which lands on the same part.
 */
internal fun openingDayPart(parts: List<DayPartSlots>, selectedTime: String): DayPart =
    DayPart.of(selectedTime)?.takeIf { part -> parts.any { it.part == part && it.bookable } }
        ?: parts.firstOrNull { it.bookable }?.part
        ?: DayPart.Morning

@Composable
fun WhenWhereStep(
    state: BookingState,
    onUpdate: (BookingState) -> Unit,
    onPickAddressOnMap: () -> Unit = {},
    expressWaiver: ExpressWaiver = ExpressWaiver.None,
) {
    // Weekday labels come from the ACTIVE app locale, not the JVM default, so a
    // per-app language override is honoured. Both inputs are remember keys: with
    // no keys the strip would keep its old English labels after a language switch.
    val locale = LocalConfiguration.current.locales.get(0) ?: Locale.getDefault()
    val todayLabel = stringResource(R.string.booking_today)
    val days = androidx.compose.runtime.remember(locale, todayLabel) { buildDays(locale, todayLabel) }

    // The strip's labels are CLDR-derived, so changing the app language rebuilds every one of them.
    // The picked day's identity ([BookingState.selectedLocalDate]) is unaffected, but the label kept
    // for display would otherwise stay in the old language and show up that way in the Confirm
    // summary. Re-derive it whenever the strip is rebuilt.
    androidx.compose.runtime.LaunchedEffect(days) {
        val picked = days.firstOrNull { it.localDate == state.selectedLocalDate }
        if (picked != null && picked.label != state.selectedDate) {
            onUpdate(state.copy(selectedDate = picked.label))
        }
    }

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
                val selected = state.selectedLocalDate == day.localDate
                DayChipView(day, selected) {
                    if (day.available) {
                        val instant = if (state.selectedTime.isNotBlank()) {
                            combineDateAndTime(day.localDate, state.selectedTime)
                        } else null
                        onUpdate(
                            state.copy(
                                selectedDate = day.label,
                                selectedLocalDate = day.localDate,
                                selectedInstant = instant,
                            ),
                        )
                    }
                }
            }
        }

        Spacer(Modifier.height(24.dp))

        // ── TIME — the part of day, then that part's quarter hours ──
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
        val pickedDayChip = days.firstOrNull { it.localDate == state.selectedLocalDate }
        val daySlots = androidx.compose.runtime.remember(pickedDayChip?.localDate) {
            pickedDayChip?.localDate?.let { timeSlotsFor(it) } ?: emptyList()
        }

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

        // Nothing until a day is picked, as on iOS.
        if (pickedDayChip != null) {
            Column(
                modifier = Modifier.padding(horizontal = 20.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                if (daySlots.none { it.state != SlotState.Unavailable }) {
                    Text(
                        stringResource(R.string.booking_all_slots_booked),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(vertical = 16.dp),
                    )
                } else {
                    DayPartTimePicker(
                        slots = daySlots,
                        selectedTime = state.selectedTime,
                        resetKey = pickedDayChip.localDate,
                        waiverAvailable = expressWaiver.status == ExpressWaiverStatus.Available,
                        onSelect = { time ->
                            onUpdate(
                                state.copy(
                                    selectedTime = time,
                                    selectedInstant = combineDateAndTime(pickedDayChip.localDate, time),
                                ),
                            )
                        },
                    )
                }
                // The express verdict belongs at the moment of choice, not at payment: a member who
                // learns their waiver is gone while confirming the price has already been surprised.
                if (daySlots.any { it.state == SlotState.Express }) {
                    ExpressWaiverNote(expressWaiver)
                }
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

/* ── Express waiver disclosure — one line under the slot grid ── */

/**
 * Two server-decided states, and [ExpressWaiverStatus.None] deliberately says nothing: for a guest
 * or a plan without the perk, the slot's own "+20%" tag already discloses the charge, and inventing a
 * third sentence for them would change a flow this ticket must leave alone.
 */
@Composable
private fun ExpressWaiverNote(waiver: ExpressWaiver) {
    val text = when (waiver.status) {
        ExpressWaiverStatus.Available ->
            stringResource(R.string.booking_express_waiver_available, waiver.remaining)
        ExpressWaiverStatus.Exhausted -> stringResource(R.string.booking_express_waiver_used)
        ExpressWaiverStatus.None -> return
    }
    Row(
        modifier = Modifier.padding(top = 4.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Icon(
            if (waiver.status == ExpressWaiverStatus.Available) Icons.Outlined.Bolt else Icons.Outlined.Info,
            null,
            tint = if (waiver.status == ExpressWaiverStatus.Available) {
                ExpressOrange
            } else {
                MaterialTheme.colorScheme.onSurfaceVariant
            },
            modifier = Modifier.size(14.dp),
        )
        Spacer(Modifier.width(6.dp))
        Text(
            text,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

/* ── Part of day, then a 4 × 4 grid of its quarter hours ── */

private val ExpressOrange = androidx.compose.ui.graphics.Color(0xFFEA580C)

/**
 * Three part-of-day buttons, each with its first and last arrival, over a 4 × 4 grid of the chosen
 * part's slots — the web wizard's time step (/customer-app/ordering-flow#step-2-date-time). Choosing a
 * part never changes the booked time; it only changes which sixteen slots are on screen. The step opens
 * on the part that holds the booked time, which carries a dot while another part is browsed; a part with
 * nothing bookable is disabled, and so is a slot inside the lead time. Shared with the recurring
 * schedule's time step, whose slots are all [SlotState.Available].
 *
 * [resetKey] drops the part being browsed, e.g. a new day reopens on the part holding the booked time.
 */
@Composable
internal fun DayPartTimePicker(
    slots: List<TimeSlot>,
    selectedTime: String,
    onSelect: (String) -> Unit,
    modifier: Modifier = Modifier,
    resetKey: Any? = null,
    waiverAvailable: Boolean = false,
) {
    val parts = androidx.compose.runtime.remember(slots) { groupByDayPart(slots) }
    var browsed by androidx.compose.runtime.remember(resetKey, selectedTime) {
        androidx.compose.runtime.mutableStateOf<DayPart?>(null)
    }
    val active = browsed?.takeIf { part -> parts.any { it.part == part && it.bookable } }
        ?: openingDayPart(parts, selectedTime)
    val holding = DayPart.of(selectedTime)
    val expressLabel = stringResource(if (waiverAvailable) R.string.booking_slot_express_waived else R.string.booking_slot_express)

    Column(modifier = modifier, verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(IntrinsicSize.Min),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            parts.forEach { part ->
                DayPartChip(
                    part = part,
                    active = part.part == active,
                    holdsSelection = part.part == holding && part.part != active,
                    onClick = { browsed = part.part },
                    modifier = Modifier
                        .weight(1f)
                        .fillMaxHeight(),
                )
            }
        }
        Spacer(Modifier.height(4.dp))
        val visible = parts.first { it.part == active }.slots
        visible.chunked(4).forEach { row ->
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                row.forEach { slot ->
                    TimeSlotChip(
                        slot = slot,
                        selected = slot.time == selectedTime,
                        expressLabel = expressLabel,
                        onClick = { onSelect(slot.time) },
                        modifier = Modifier.weight(1f),
                    )
                }
            }
        }
        // The grid marks an express slot with a bolt; this says what the bolt costs.
        if (visible.any { it.state == SlotState.Express }) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Outlined.Bolt, null, tint = ExpressOrange, modifier = Modifier.size(14.dp))
                Spacer(Modifier.width(6.dp))
                Text(
                    expressLabel,
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = ExpressOrange,
                )
            }
        }
    }
}

/** A part-of-day button: its name over its first and last arrival ("08:00–11:45"). */
@Composable
private fun DayPartChip(
    part: DayPartSlots,
    active: Boolean,
    holdsSelection: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val name = stringResource(
        when (part.part) {
            DayPart.Morning -> R.string.booking_day_part_morning
            DayPart.Afternoon -> R.string.booking_day_part_afternoon
            DayPart.Evening -> R.string.booking_day_part_evening
        },
    )
    val description = androidx.compose.ui.res.pluralStringResource(
        R.plurals.booking_day_part_a11y,
        part.bookableCount,
        name,
        part.bookableCount,
    )
    val enabled = part.bookable
    val shape = RoundedCornerShape(12.dp)
    val alpha = if (enabled) 1f else 0.38f
    Column(
        modifier = modifier
            .heightIn(min = 48.dp)
            .clip(shape)
            .background(if (active) selectionTint() else MaterialTheme.colorScheme.surface)
            .border(
                width = if (active) 2.dp else 1.dp,
                color = if (active) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                shape = shape,
            )
            .selectable(selected = active, enabled = enabled, role = Role.Tab, onClick = onClick)
            .semantics { contentDescription = description }
            .padding(horizontal = 4.dp, vertical = 8.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                name,
                style = MaterialTheme.typography.titleSmall,
                color = (if (active) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurface).copy(alpha = alpha),
                textAlign = TextAlign.Center,
            )
            if (holdsSelection) {
                Spacer(Modifier.width(4.dp))
                Box(Modifier.size(6.dp).background(MaterialTheme.colorScheme.primary, CircleShape))
            }
        }
        Text(
            "${part.slots.firstOrNull()?.time.orEmpty()}–${part.slots.lastOrNull()?.time.orEmpty()}",
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = alpha),
            textAlign = TextAlign.Center,
        )
    }
}

/** One arrival time in the grid: the time, with a bolt when it is an express slot. */
@Composable
private fun TimeSlotChip(
    slot: TimeSlot,
    selected: Boolean,
    expressLabel: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val enabled = slot.state != SlotState.Unavailable
    val isExpress = slot.state == SlotState.Express
    val shape = RoundedCornerShape(12.dp)
    val textColor = when {
        selected -> MaterialTheme.colorScheme.primary
        enabled -> MaterialTheme.colorScheme.onSurface
        else -> MaterialTheme.colorScheme.onSurface.copy(alpha = 0.38f)
    }
    Row(
        modifier = modifier
            .height(48.dp)
            .clip(shape)
            .background(
                when {
                    selected -> selectionTint()
                    enabled -> MaterialTheme.colorScheme.surface
                    else -> MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f)
                },
            )
            .border(
                width = if (selected) 2.dp else 1.dp,
                color = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                shape = shape,
            )
            .selectable(selected = selected, enabled = enabled, role = Role.RadioButton, onClick = onClick)
            .semantics { contentDescription = if (isExpress) "${slot.time}, $expressLabel" else slot.time },
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (isExpress) {
            Icon(Icons.Outlined.Bolt, null, tint = ExpressOrange, modifier = Modifier.size(12.dp))
            Spacer(Modifier.width(2.dp))
        }
        Text(
            slot.time,
            style = MaterialTheme.typography.titleSmall,
            color = textColor,
            maxLines = 1,
        )
    }
}

@Composable
private fun SectionLabel(text: String, modifier: Modifier = Modifier) {
    Text(text, style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold), color = MaterialTheme.colorScheme.onBackground, modifier = modifier)
}
