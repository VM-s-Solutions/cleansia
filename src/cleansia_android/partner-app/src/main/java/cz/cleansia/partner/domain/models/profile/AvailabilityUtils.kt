package cz.cleansia.partner.domain.models.profile

import java.time.LocalDate
import java.time.LocalTime
import java.time.YearMonth
import java.time.format.DateTimeFormatter
import java.time.temporal.ChronoUnit
import java.util.Calendar

/**
 * Resolved schedule for a specific date, combining weekly schedule and date overrides
 */
data class ResolvedDaySchedule(
    val date: LocalDate,
    val isWorkingDay: Boolean,
    val timeSlots: List<TimeSlot>,
    val isOverride: Boolean,
    val overrideNote: String? = null
)

/**
 * Today's working info for dashboard display
 */
data class TodayWorkingInfo(
    val isWorkingDay: Boolean,
    val isOverrideDay: Boolean,
    val timeSlots: List<TimeSlot>,
    val overrideNote: String? = null,
    val totalMinutes: Int,
    val elapsedMinutes: Int,
    val progressFraction: Float
)

/**
 * Utility functions for resolving availability schedules
 */
object AvailabilityUtils {

    private val dateFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd")

    /**
     * Resolve the schedule for a specific date.
     * Checks date overrides first, then falls back to the weekly schedule.
     */
    fun resolveScheduleForDate(
        date: LocalDate,
        weeklySchedule: List<DayAvailability>,
        dateOverrides: List<DateOverride>
    ): ResolvedDaySchedule {
        val dateStr = date.format(dateFormatter)

        // Check for date override first
        val override = dateOverrides.find { it.date == dateStr }
        if (override != null) {
            return ResolvedDaySchedule(
                date = date,
                isWorkingDay = override.isAvailable,
                timeSlots = if (override.isAvailable) override.timeSlots else emptyList(),
                isOverride = true,
                overrideNote = override.note
            )
        }

        // Fall back to weekly schedule
        val calendarDayOfWeek = mapLocalDateDayToCalendar(date)
        val daySchedule = weeklySchedule.find { it.dayOfWeek == calendarDayOfWeek }

        return ResolvedDaySchedule(
            date = date,
            isWorkingDay = daySchedule?.isAvailable == true,
            timeSlots = if (daySchedule?.isAvailable == true) daySchedule.effectiveTimeSlots() else emptyList(),
            isOverride = false
        )
    }

    /**
     * Resolve schedules for an entire month
     */
    fun resolveScheduleForMonth(
        yearMonth: YearMonth,
        weeklySchedule: List<DayAvailability>,
        dateOverrides: List<DateOverride>
    ): Map<LocalDate, ResolvedDaySchedule> {
        val result = mutableMapOf<LocalDate, ResolvedDaySchedule>()
        val daysInMonth = yearMonth.lengthOfMonth()

        for (day in 1..daysInMonth) {
            val date = yearMonth.atDay(day)
            result[date] = resolveScheduleForDate(date, weeklySchedule, dateOverrides)
        }

        return result
    }

    /**
     * Get today's working info for dashboard display
     */
    fun getTodayWorkingInfo(
        weeklySchedule: List<DayAvailability>,
        dateOverrides: List<DateOverride>
    ): TodayWorkingInfo {
        val today = LocalDate.now()
        val resolved = resolveScheduleForDate(today, weeklySchedule, dateOverrides)

        val totalMinutes = calculateTotalMinutes(resolved.timeSlots)
        val elapsedMinutes = calculateElapsedMinutes(resolved.timeSlots)
        val progressFraction = if (totalMinutes > 0) {
            (elapsedMinutes.toFloat() / totalMinutes).coerceIn(0f, 1f)
        } else {
            0f
        }

        return TodayWorkingInfo(
            isWorkingDay = resolved.isWorkingDay,
            isOverrideDay = resolved.isOverride,
            timeSlots = resolved.timeSlots,
            overrideNote = resolved.overrideNote,
            totalMinutes = totalMinutes,
            elapsedMinutes = elapsedMinutes,
            progressFraction = progressFraction
        )
    }

    /**
     * Calculate total working minutes from time slots
     */
    private fun calculateTotalMinutes(slots: List<TimeSlot>): Int {
        return slots.sumOf { slot ->
            val start = parseTime(slot.startTime)
            val end = parseTime(slot.endTime)
            ChronoUnit.MINUTES.between(start, end).toInt().coerceAtLeast(0)
        }
    }

    /**
     * Calculate elapsed working minutes from time slots based on current time
     */
    private fun calculateElapsedMinutes(slots: List<TimeSlot>): Int {
        val now = LocalTime.now()
        var elapsed = 0

        for (slot in slots) {
            val start = parseTime(slot.startTime)
            val end = parseTime(slot.endTime)

            if (now.isAfter(end) || now == end) {
                // Slot fully passed
                elapsed += ChronoUnit.MINUTES.between(start, end).toInt().coerceAtLeast(0)
            } else if (now.isAfter(start)) {
                // Currently in this slot
                elapsed += ChronoUnit.MINUTES.between(start, now).toInt().coerceAtLeast(0)
            }
            // If now is before start, nothing elapsed for this slot
        }

        return elapsed
    }

    private fun parseTime(time: String): LocalTime {
        return try {
            val parts = time.split(":")
            LocalTime.of(parts[0].toInt(), parts[1].toInt())
        } catch (e: Exception) {
            LocalTime.of(9, 0)
        }
    }

    /**
     * Map java.time DayOfWeek to java.util.Calendar day constant
     */
    private fun mapLocalDateDayToCalendar(date: LocalDate): Int {
        return when (date.dayOfWeek) {
            java.time.DayOfWeek.MONDAY -> Calendar.MONDAY
            java.time.DayOfWeek.TUESDAY -> Calendar.TUESDAY
            java.time.DayOfWeek.WEDNESDAY -> Calendar.WEDNESDAY
            java.time.DayOfWeek.THURSDAY -> Calendar.THURSDAY
            java.time.DayOfWeek.FRIDAY -> Calendar.FRIDAY
            java.time.DayOfWeek.SATURDAY -> Calendar.SATURDAY
            java.time.DayOfWeek.SUNDAY -> Calendar.SUNDAY
        }
    }

    // ===== API ↔ UI Conversion =====

    private val dayNameToCalendar = mapOf(
        "Monday" to Calendar.MONDAY,
        "Tuesday" to Calendar.TUESDAY,
        "Wednesday" to Calendar.WEDNESDAY,
        "Thursday" to Calendar.THURSDAY,
        "Friday" to Calendar.FRIDAY,
        "Saturday" to Calendar.SATURDAY,
        "Sunday" to Calendar.SUNDAY
    )

    private val calendarToDayName = dayNameToCalendar.entries.associate { (k, v) -> v to k }

    /**
     * Convert API availability format to UI DayAvailability list.
     * API: Map<"Monday", List<AvailabilityTimeRange>> → UI: List<DayAvailability>
     * Only processes day-name keys; date keys are handled by apiToUiDateOverrides.
     */
    fun apiToUiAvailability(api: Map<String, List<AvailabilityTimeRange>>?): List<DayAvailability> {
        if (api.isNullOrEmpty()) return emptyList()

        return dayNameToCalendar.map { (dayName, calendarDay) ->
            val ranges = api[dayName]
            if (ranges != null && ranges.isNotEmpty()) {
                DayAvailability(
                    dayOfWeek = calendarDay,
                    isAvailable = true,
                    timeSlots = ranges.map { TimeSlot(startTime = it.start, endTime = it.end) }
                )
            } else {
                DayAvailability(
                    dayOfWeek = calendarDay,
                    isAvailable = false,
                    timeSlots = listOf(TimeSlot())
                )
            }
        }
    }

    /**
     * Extract date overrides from API availability format.
     * Date override keys are in "yyyy-MM-dd" format (not day names).
     * Empty time ranges = day off; non-empty = custom schedule for that date.
     */
    fun apiToUiDateOverrides(api: Map<String, List<AvailabilityTimeRange>>?): List<DateOverride> {
        if (api.isNullOrEmpty()) return emptyList()

        val dayNames = dayNameToCalendar.keys
        return api.entries
            .filter { (key, _) -> key !in dayNames }
            .mapNotNull { (key, ranges) ->
                // Verify key is a valid date
                try {
                    LocalDate.parse(key, dateFormatter)
                    DateOverride(
                        date = key,
                        isAvailable = ranges.isNotEmpty(),
                        timeSlots = if (ranges.isNotEmpty()) {
                            ranges.map { TimeSlot(startTime = it.start, endTime = it.end) }
                        } else {
                            emptyList()
                        }
                    )
                } catch (_: Exception) {
                    null
                }
            }
            .sortedBy { it.date }
    }

    /**
     * Convert UI date overrides to API format entries.
     * Day off → date key with empty list. Custom hours → date key with time ranges.
     */
    fun uiToApiDateOverrides(overrides: List<DateOverride>): Map<String, List<AvailabilityTimeRange>> {
        val result = mutableMapOf<String, List<AvailabilityTimeRange>>()
        for (override in overrides) {
            result[override.date] = if (override.isAvailable) {
                override.timeSlots.map { AvailabilityTimeRange(start = it.startTime, end = it.endTime) }
            } else {
                emptyList()
            }
        }
        return result
    }

    /**
     * Convert UI DayAvailability list to API availability format.
     * UI: List<DayAvailability> → API: Map<"Monday", List<AvailabilityTimeRange>>
     * Only includes days that are marked as available.
     */
    fun uiToApiAvailability(ui: List<DayAvailability>): Map<String, List<AvailabilityTimeRange>> {
        val result = mutableMapOf<String, List<AvailabilityTimeRange>>()

        for (day in ui) {
            if (!day.isAvailable) continue
            val dayName = calendarToDayName[day.dayOfWeek] ?: continue
            val slots = day.effectiveTimeSlots()
            result[dayName] = slots.map { AvailabilityTimeRange(start = it.startTime, end = it.endTime) }
        }

        return result
    }

    /**
     * Format time slots for display (e.g. "9:00 - 17:00")
     */
    fun formatTimeSlots(slots: List<TimeSlot>): String {
        return slots.joinToString(", ") { "${it.startTime} - ${it.endTime}" }
    }

    /**
     * Format minutes to hours and minutes display (e.g. "8h 30m")
     */
    fun formatMinutesToDisplay(minutes: Int): String {
        val hours = minutes / 60
        val mins = minutes % 60
        return when {
            hours > 0 && mins > 0 -> "${hours}h ${mins}m"
            hours > 0 -> "${hours}h"
            else -> "${mins}m"
        }
    }
}
