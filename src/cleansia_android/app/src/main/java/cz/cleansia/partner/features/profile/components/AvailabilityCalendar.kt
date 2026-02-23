package cz.cleansia.partner.features.profile.components

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.domain.models.profile.AvailabilityUtils
import cz.cleansia.partner.domain.models.profile.DateOverride
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.TimeSlot
import cz.cleansia.partner.features.profile.components.availability.CalendarGrid
import cz.cleansia.partner.features.profile.components.availability.CalendarLegend
import cz.cleansia.partner.features.profile.components.availability.DayDetailPanel
import cz.cleansia.partner.features.profile.components.availability.DayOfWeekHeaders
import cz.cleansia.partner.features.profile.components.availability.MonthHeader
import java.time.LocalDate
import java.time.YearMonth
import java.time.format.DateTimeFormatter

/**
 * Calendar-based availability view with month navigation and day detail panel
 */
@Composable
fun AvailabilityCalendarView(
    availability: List<DayAvailability>,
    dateOverrides: List<DateOverride>,
    selectedDate: LocalDate,
    currentMonth: YearMonth,
    isEditing: Boolean,
    onDateSelected: (LocalDate) -> Unit,
    onMonthChanged: (YearMonth) -> Unit,
    onEditHours: (LocalDate, List<TimeSlot>) -> Unit,
    onMarkException: (DateOverride) -> Unit,
    onRemoveException: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    val resolvedMonth = remember(currentMonth, availability, dateOverrides) {
        AvailabilityUtils.resolveScheduleForMonth(currentMonth, availability, dateOverrides)
    }

    Column(modifier = modifier.fillMaxWidth()) {
        // Month header with navigation
        MonthHeader(
            currentMonth = currentMonth,
            onPreviousMonth = { onMonthChanged(currentMonth.minusMonths(1)) },
            onNextMonth = { onMonthChanged(currentMonth.plusMonths(1)) }
        )

        Spacer(modifier = Modifier.height(12.dp))

        // Day-of-week headers
        DayOfWeekHeaders()

        Spacer(modifier = Modifier.height(8.dp))

        // Calendar grid
        CalendarGrid(
            currentMonth = currentMonth,
            resolvedMonth = resolvedMonth,
            selectedDate = selectedDate,
            onDateSelected = onDateSelected
        )

        Spacer(modifier = Modifier.height(8.dp))

        // Legend
        CalendarLegend()

        Spacer(modifier = Modifier.height(16.dp))

        // Day detail panel
        val selectedSchedule = resolvedMonth[selectedDate]
        if (selectedSchedule != null) {
            DayDetailPanel(
                schedule = selectedSchedule,
                isEditing = isEditing,
                onEditHours = { slots -> onEditHours(selectedDate, slots) },
                onMarkException = { override -> onMarkException(override) },
                onRemoveException = { onRemoveException(selectedDate.format(DateTimeFormatter.ofPattern("yyyy-MM-dd"))) }
            )
        }
    }
}
