package cz.cleansia.partner.core.utils

import android.text.format.DateFormat
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext
import java.time.Instant
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.DateTimeParseException
import java.time.format.FormatStyle
import java.util.Locale

/**
 * Utility object for handling date and time formatting with localization support.
 *
 * Uses the device's locale settings to format dates and times appropriately.
 * For example:
 * - Czech locale: "27. 1. 2026" for dates, "14:30" for time
 * - US locale: "1/27/2026" for dates, "2:30 PM" for time
 */
object DateTimeUtils {

    // API date formats (ISO 8601)
    private val apiDateFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd")
    private val apiDateTimeFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss")
    private val apiDateTimeWithMillisFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss.SSS")

    /**
     * Format a date string from API format to localized display format.
     *
     * @param apiDateString Date string in API format (yyyy-MM-dd or ISO datetime)
     * @param locale The locale to use for formatting (defaults to system locale)
     * @return Localized date string, or original string if parsing fails
     */
    fun formatDate(apiDateString: String?, locale: Locale = Locale.getDefault()): String {
        if (apiDateString.isNullOrBlank()) return ""

        return try {
            val localDate = parseToLocalDate(apiDateString)
            if (localDate != null) {
                val formatter = DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(locale)
                localDate.format(formatter)
            } else {
                apiDateString
            }
        } catch (e: Exception) {
            apiDateString
        }
    }

    /**
     * Format a date string to a short localized format (e.g., "27. 1." or "1/27").
     */
    fun formatDateShort(apiDateString: String?, locale: Locale = Locale.getDefault()): String {
        if (apiDateString.isNullOrBlank()) return ""

        return try {
            val localDate = parseToLocalDate(apiDateString)
            if (localDate != null) {
                val formatter = DateTimeFormatter.ofLocalizedDate(FormatStyle.SHORT).withLocale(locale)
                localDate.format(formatter)
            } else {
                apiDateString
            }
        } catch (e: Exception) {
            apiDateString
        }
    }

    /**
     * Format a datetime string from API format to localized display format.
     *
     * @param apiDateTimeString DateTime string in API format
     * @param locale The locale to use for formatting
     * @return Localized datetime string
     */
    fun formatDateTime(apiDateTimeString: String?, locale: Locale = Locale.getDefault()): String {
        if (apiDateTimeString.isNullOrBlank()) return ""

        return try {
            val localDateTime = parseToLocalDateTime(apiDateTimeString)
            if (localDateTime != null) {
                val formatter = DateTimeFormatter.ofLocalizedDateTime(FormatStyle.MEDIUM, FormatStyle.SHORT).withLocale(locale)
                localDateTime.format(formatter)
            } else {
                apiDateTimeString
            }
        } catch (e: Exception) {
            apiDateTimeString
        }
    }

    /**
     * Format a datetime string showing only date and time in a compact format.
     * E.g., "27. 1. 2026, 14:30" for Czech or "1/27/2026, 2:30 PM" for US
     */
    fun formatDateTimeCompact(apiDateTimeString: String?, locale: Locale = Locale.getDefault()): String {
        if (apiDateTimeString.isNullOrBlank()) return ""

        return try {
            val localDateTime = parseToLocalDateTime(apiDateTimeString)
            if (localDateTime != null) {
                val dateFormatter = DateTimeFormatter.ofLocalizedDate(FormatStyle.SHORT).withLocale(locale)
                val timeFormatter = DateTimeFormatter.ofLocalizedTime(FormatStyle.SHORT).withLocale(locale)
                "${localDateTime.format(dateFormatter)}, ${localDateTime.format(timeFormatter)}"
            } else {
                apiDateTimeString
            }
        } catch (e: Exception) {
            apiDateTimeString
        }
    }

    /**
     * Format time only from a datetime string.
     *
     * @param apiDateTimeString DateTime string in API format
     * @param locale The locale to use for formatting
     * @return Localized time string (e.g., "14:30" or "2:30 PM")
     */
    fun formatTime(apiDateTimeString: String?, locale: Locale = Locale.getDefault()): String {
        if (apiDateTimeString.isNullOrBlank()) return ""

        return try {
            val localDateTime = parseToLocalDateTime(apiDateTimeString)
            if (localDateTime != null) {
                val formatter = DateTimeFormatter.ofLocalizedTime(FormatStyle.SHORT).withLocale(locale)
                localDateTime.format(formatter)
            } else {
                apiDateTimeString
            }
        } catch (e: Exception) {
            apiDateTimeString
        }
    }

    /**
     * Format a time string (HH:mm) to localized format.
     */
    fun formatTimeOnly(timeString: String?, locale: Locale = Locale.getDefault()): String {
        if (timeString.isNullOrBlank()) return ""

        return try {
            val localTime = LocalTime.parse(timeString, DateTimeFormatter.ofPattern("HH:mm"))
            val formatter = DateTimeFormatter.ofLocalizedTime(FormatStyle.SHORT).withLocale(locale)
            localTime.format(formatter)
        } catch (e: Exception) {
            timeString
        }
    }

    /**
     * Format a date showing day of week, date and time.
     * E.g., "Monday, January 27, 2026 at 2:30 PM" or "pondělí 27. ledna 2026 14:30"
     */
    fun formatDateTimeFull(apiDateTimeString: String?, locale: Locale = Locale.getDefault()): String {
        if (apiDateTimeString.isNullOrBlank()) return ""

        return try {
            val localDateTime = parseToLocalDateTime(apiDateTimeString)
            if (localDateTime != null) {
                val formatter = DateTimeFormatter.ofLocalizedDateTime(FormatStyle.FULL, FormatStyle.SHORT).withLocale(locale)
                localDateTime.format(formatter)
            } else {
                apiDateTimeString
            }
        } catch (e: Exception) {
            apiDateTimeString
        }
    }

    /**
     * Format date showing day of week and date only (no time).
     * E.g., "Monday, January 27" or "pondělí 27. ledna"
     */
    fun formatDateWithDayOfWeek(apiDateString: String?, locale: Locale = Locale.getDefault()): String {
        if (apiDateString.isNullOrBlank()) return ""

        return try {
            val localDate = parseToLocalDate(apiDateString)
            if (localDate != null) {
                val formatter = DateTimeFormatter.ofLocalizedDate(FormatStyle.FULL).withLocale(locale)
                localDate.format(formatter)
            } else {
                apiDateString
            }
        } catch (e: Exception) {
            apiDateString
        }
    }

    /**
     * Get relative date description (Today, Tomorrow, Yesterday, or formatted date).
     */
    fun formatRelativeDate(apiDateString: String?, locale: Locale = Locale.getDefault()): String {
        if (apiDateString.isNullOrBlank()) return ""

        return try {
            val localDate = parseToLocalDate(apiDateString) ?: return apiDateString
            val today = LocalDate.now()

            when {
                localDate.isEqual(today) -> getLocalizedToday(locale)
                localDate.isEqual(today.plusDays(1)) -> getLocalizedTomorrow(locale)
                localDate.isEqual(today.minusDays(1)) -> getLocalizedYesterday(locale)
                else -> formatDate(apiDateString, locale)
            }
        } catch (e: Exception) {
            apiDateString
        }
    }

    /**
     * Parse API date/datetime string to LocalDate.
     */
    fun parseToLocalDate(apiString: String): LocalDate? {
        if (apiString.isBlank()) return null

        // Try parsing as full datetime first
        val localDateTime = parseToLocalDateTime(apiString)
        if (localDateTime != null) {
            return localDateTime.toLocalDate()
        }

        // Try parsing as date only
        return try {
            LocalDate.parse(apiString, apiDateFormatter)
        } catch (e: DateTimeParseException) {
            null
        }
    }

    /**
     * Parse API datetime string to LocalDateTime.
     */
    fun parseToLocalDateTime(apiString: String): LocalDateTime? {
        if (apiString.isBlank()) return null

        // Try various formats
        val formatters = listOf(
            apiDateTimeWithMillisFormatter,
            apiDateTimeFormatter,
            DateTimeFormatter.ISO_LOCAL_DATE_TIME,
            DateTimeFormatter.ISO_DATE_TIME
        )

        for (formatter in formatters) {
            try {
                return LocalDateTime.parse(apiString.substringBefore('Z').substringBefore('+'), formatter)
            } catch (e: DateTimeParseException) {
                continue
            }
        }

        // Try parsing as Instant
        try {
            val instant = Instant.parse(apiString)
            return LocalDateTime.ofInstant(instant, ZoneId.systemDefault())
        } catch (e: DateTimeParseException) {
            // Ignore
        }

        return null
    }

    /**
     * Format LocalDate to API format (yyyy-MM-dd).
     */
    fun toApiFormat(date: LocalDate): String {
        return date.format(apiDateFormatter)
    }

    /**
     * Format LocalDateTime to API format.
     */
    fun toApiFormat(dateTime: LocalDateTime): String {
        return dateTime.format(apiDateTimeFormatter)
    }

    // Localized strings for relative dates
    private fun getLocalizedToday(locale: Locale): String {
        return when (locale.language) {
            "cs" -> "Dnes"
            "sk" -> "Dnes"
            "de" -> "Heute"
            "pl" -> "Dzisiaj"
            else -> "Today"
        }
    }

    private fun getLocalizedTomorrow(locale: Locale): String {
        return when (locale.language) {
            "cs" -> "Zítra"
            "sk" -> "Zajtra"
            "de" -> "Morgen"
            "pl" -> "Jutro"
            else -> "Tomorrow"
        }
    }

    private fun getLocalizedYesterday(locale: Locale): String {
        return when (locale.language) {
            "cs" -> "Včera"
            "sk" -> "Včera"
            "de" -> "Gestern"
            "pl" -> "Wczoraj"
            else -> "Yesterday"
        }
    }
}

/**
 * Composable function to get formatted date using the device's 24-hour preference.
 */
@Composable
fun rememberDateTimeFormatter(): DateTimeUtils {
    val context = LocalContext.current
    val is24HourFormat = DateFormat.is24HourFormat(context)
    // The DateTimeUtils already uses locale-aware formatting which respects 24-hour format
    return DateTimeUtils
}
