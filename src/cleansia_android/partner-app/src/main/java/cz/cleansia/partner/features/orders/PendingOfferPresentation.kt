package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.PendingOfferItem
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

/** Which calendar day, in the rendering zone, the reservation ends on. */
enum class RespondByDay { Today, Tomorrow, Later, Ended }

data class RespondBy(val day: RespondByDay, val time: String, val date: String)

/**
 * The hold's real expiry lives on the server, so a client-side countdown drifts into a lie the moment
 * the screen sits idle. This resolves the server's instant to a wall-clock label plus the day it falls
 * on, and the composable turns that into a sentence.
 */
fun respondBy(
    iso: String?,
    nowMillis: Long,
    zone: ZoneId = ZoneId.systemDefault(),
    locale: Locale = Locale.getDefault(),
): RespondBy? {
    val instant = parseInstant(iso) ?: return null
    val local = instant.atZone(zone)
    val time = local.format(DateTimeFormatter.ofPattern("HH:mm", locale))
    val date = local.format(DateTimeFormatter.ofPattern("MMM d", locale))

    if (instant.toEpochMilli() <= nowMillis) return RespondBy(RespondByDay.Ended, time, date)

    val today = Instant.ofEpochMilli(nowMillis).atZone(zone).toLocalDate()
    val day = when (local.toLocalDate()) {
        today -> RespondByDay.Today
        today.plusDays(1) -> RespondByDay.Tomorrow
        else -> RespondByDay.Later
    }
    return RespondBy(day, time, date)
}

/** The offer whose reservation ends first — the one the cleaner has least time to answer. */
fun soonestOffer(offers: List<PendingOfferItem>): PendingOfferItem? =
    offers.mapNotNull { offer -> parseInstant(offer.respondByUtc)?.let { offer to it } }
        .minByOrNull { it.second }
        ?.first

private fun parseInstant(iso: String?): Instant? {
    if (iso.isNullOrBlank()) return null
    return runCatching { Instant.parse(iso) }.getOrNull()
}
