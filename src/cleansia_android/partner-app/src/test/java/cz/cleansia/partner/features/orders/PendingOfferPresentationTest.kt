package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.PendingOfferItem
import java.time.Instant
import java.time.ZoneId
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The deadline is rendered as an instant and never as a remaining time: the hold's real expiry is
 * server-side, so "expires in 12 minutes" on a screen that has been open for twenty is a lie the
 * client cannot detect. Everything here therefore resolves to a wall-clock label plus the calendar
 * day it falls on, and the caller supplies `now` so the classification is assertable.
 */
class PendingOfferPresentationTest {

    private val utc = ZoneId.of("UTC")
    private val en = Locale.ENGLISH

    private fun at(iso: String): Long = Instant.parse(iso).toEpochMilli()

    @Test
    fun `a deadline later the same local day is Today and carries its wall-clock time`() {
        val respondBy = respondBy("2026-08-10T18:40:00Z", at("2026-08-10T09:00:00Z"), utc, en)

        assertEquals(RespondByDay.Today, respondBy?.day)
        assertEquals("18:40", respondBy?.time)
    }

    @Test
    fun `the day is the local calendar day, not a 24-hour delta`() {
        // Three hours away, but on the next calendar day in the rendering zone. A delta-based
        // classification says "today" here and the cleaner reads a deadline that has already passed
        // by the time they look at their own calendar.
        val respondBy = respondBy("2026-08-11T01:00:00Z", at("2026-08-10T22:00:00Z"), utc, en)

        assertEquals(RespondByDay.Tomorrow, respondBy?.day)
    }

    @Test
    fun `a deadline further out is Later and carries a date as well as a time`() {
        val respondBy = respondBy("2026-08-14T07:05:00Z", at("2026-08-10T09:00:00Z"), utc, en)

        assertEquals(RespondByDay.Later, respondBy?.day)
        assertEquals("07:05", respondBy?.time)
        assertEquals("Aug 14", respondBy?.date)
    }

    @Test
    fun `a deadline already reached is Ended, never a time the cleaner could still act on`() {
        // The server's own predicate is `PreferredHoldUntilUtc > nowUtc`, so equality is over.
        assertEquals(
            RespondByDay.Ended,
            respondBy("2026-08-10T09:00:00Z", at("2026-08-10T09:00:00Z"), utc, en)?.day,
        )
        assertEquals(
            RespondByDay.Ended,
            respondBy("2026-08-10T08:59:59Z", at("2026-08-10T09:00:00Z"), utc, en)?.day,
        )
    }

    @Test
    fun `an absent or unparseable deadline resolves to nothing rather than a guess`() {
        assertNull(respondBy(null, at("2026-08-10T09:00:00Z"), utc, en))
        assertNull(respondBy("", at("2026-08-10T09:00:00Z"), utc, en))
        assertNull(respondBy("not-a-date", at("2026-08-10T09:00:00Z"), utc, en))
    }

    @Test
    fun `the deadline is rendered in the requested zone, not in UTC`() {
        val prague = ZoneId.of("Europe/Prague")

        assertEquals("23:40", respondBy("2026-08-10T21:40:00Z", at("2026-08-10T09:00:00Z"), prague, en)?.time)
    }

    @Test
    fun `the soonest offer is the earliest deadline, not the first row the server sent`() {
        val offers = listOf(
            offer(id = "late", respondByUtc = "2026-08-10T20:00:00Z"),
            offer(id = "soon", respondByUtc = "2026-08-10T10:00:00Z"),
            offer(id = "middle", respondByUtc = "2026-08-10T15:00:00Z"),
        )

        assertEquals("soon", soonestOffer(offers)?.id)
    }

    @Test
    fun `an offer with no parseable deadline never wins the soonest slot`() {
        val offers = listOf(
            offer(id = "broken", respondByUtc = "not-a-date"),
            offer(id = "real", respondByUtc = "2026-08-10T20:00:00Z"),
        )

        assertEquals("real", soonestOffer(offers)?.id)
    }

    @Test
    fun `no offers means no soonest`() {
        assertNull(soonestOffer(emptyList()))
        assertNull(soonestOffer(listOf(offer(id = "broken", respondByUtc = null))))
    }

    /**
     * A failed handover and a failed release are different events. Sharing one string apologises for a
     * job that was never handed over, on the one screen where the platform's credibility is the whole
     * feature — and a `when` inside a composable is invisible to every check we have.
     */
    @Test
    fun `a refused confirm and a refused release do not share a word`() {
        val confirm = offerRefusalCopy(OfferAction.Confirm)
        val decline = offerRefusalCopy(OfferAction.Decline)

        assertEquals(R.string.offer_blocked_title, confirm.titleRes)
        assertEquals(R.string.offer_blocked_body, confirm.bodyRes)
        assertEquals(R.string.offer_release_failed_title, decline.titleRes)
        assertEquals(R.string.offer_release_failed_body, decline.bodyRes)
        assertNotEquals(confirm.titleRes, decline.titleRes)
        assertNotEquals(confirm.bodyRes, decline.bodyRes)
    }

    private fun offer(id: String, respondByUtc: String?) = PendingOfferItem(
        id = id,
        displayOrderNumber = "CL-$id",
        cleaningDateTime = "2026-08-12T09:00:00Z",
        estimatedTime = 120,
        respondByUtc = respondByUtc,
        customerAddressApproximate = "Praha 4 · 14000",
        rooms = 2,
        bathrooms = 1,
        totalPrice = 1200.0,
        currencyCode = "CZK",
    )
}
