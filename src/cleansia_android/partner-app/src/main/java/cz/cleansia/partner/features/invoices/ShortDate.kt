package cz.cleansia.partner.features.invoices

import java.time.LocalDate
import java.time.LocalDateTime
import java.time.ZoneId
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Locale

private val shortDateFormatter = DateTimeFormatter.ofPattern("d MMM yyyy", Locale.getDefault())

/**
 * "20 Oct 2026" for a server timestamp or date, null when it is blank or cannot be read. The day is the one the
 * timestamp's own offset names, as the invoices show it. [inDeviceZone] is the day on the phone's clock instead, and a
 * timestamp with no offset is then read as the phone's own time: how the period pay dates its lines.
 */
internal fun formatShortDate(iso: String?, inDeviceZone: Boolean = false): String? {
    if (iso.isNullOrBlank()) return null
    val date = runCatching {
        val zoned = ZonedDateTime.parse(iso)
        (if (inDeviceZone) zoned.withZoneSameInstant(ZoneId.systemDefault()) else zoned).toLocalDate()
    }.getOrNull()
        ?: (if (inDeviceZone) runCatching { LocalDateTime.parse(iso).toLocalDate() }.getOrNull() else null)
        ?: runCatching { LocalDate.parse(iso) }.getOrNull()
        ?: return null
    return date.format(shortDateFormatter)
}
