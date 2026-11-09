package cz.cleansia.partner.features.invoices

import java.time.LocalDate
import java.time.LocalDateTime
import java.time.ZoneId
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Locale

/**
 * "20 Oct 2026" for a server timestamp or date, null when it is blank or cannot be read. The day is the one on the
 * phone's clock, and a timestamp with no offset is read as the phone's own time, so an invoice and a period pay line
 * paid at the same moment show the same day.
 */
internal fun formatShortDate(iso: String?): String? {
    if (iso.isNullOrBlank()) return null
    val date = runCatching { ZonedDateTime.parse(iso).withZoneSameInstant(ZoneId.systemDefault()).toLocalDate() }.getOrNull()
        ?: runCatching { LocalDateTime.parse(iso).toLocalDate() }.getOrNull()
        ?: runCatching { LocalDate.parse(iso) }.getOrNull()
        ?: return null
    // Built per call: the in-app language can change while the process lives.
    return date.format(DateTimeFormatter.ofPattern("d MMM yyyy", Locale.getDefault()))
}
