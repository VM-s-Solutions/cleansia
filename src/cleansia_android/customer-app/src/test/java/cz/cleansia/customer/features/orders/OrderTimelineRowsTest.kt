package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.orders.OrderStatusTrackDto
import cz.cleansia.customer.core.user.CodeDto
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The history carries a row each time a seat is advertised again, so a booking two cleaners share reads
 * "Confirmed, Confirmed" after one of them leaves, and the walk-back's New reads like the booking was
 * never taken. The customer was told something different at that moment, so the timeline says it too.
 */
class OrderTimelineRowsTest {

    @Test
    fun `a status repeated by a re-advertised seat is listed once, at the moment it was reached`() {
        val rows = timelineRows(listOf(track(NEW, "10:00"), track(CONFIRMED, "11:00"), track(CONFIRMED, "12:00")))

        assertEquals(listOf(NEW, CONFIRMED), rows.map { it.track.status?.value })
        assertEquals("2026-10-20T11:00:00Z", rows[1].track.createdOn)
        assertEquals(listOf(false, false), rows.map { it.returnedToBoard })
    }

    @Test
    fun `the New after a Confirmed is the walk-back and is marked as such`() {
        val rows = timelineRows(listOf(track(NEW, "10:00"), track(CONFIRMED, "11:00"), track(NEW, "12:00")))

        assertEquals(listOf(NEW, CONFIRMED, NEW), rows.map { it.track.status?.value })
        assertEquals(listOf(false, false, true), rows.map { it.returnedToBoard })
    }

    @Test
    fun `a re-take after the walk-back keeps every pass and marks only the walk-back`() {
        val rows = timelineRows(
            listOf(track(NEW, "10:00"), track(CONFIRMED, "11:00"), track(NEW, "12:00"), track(CONFIRMED, "13:00")),
        )

        assertEquals(listOf(NEW, CONFIRMED, NEW, CONFIRMED), rows.map { it.track.status?.value })
        assertEquals(listOf(false, false, true, false), rows.map { it.returnedToBoard })
    }

    @Test
    fun `the booking's own first New is not a walk-back`() {
        val rows = timelineRows(listOf(track(NEW, "10:00"), track(CONFIRMED, "11:00"), track(IN_PROGRESS, "12:00")))

        assertEquals(listOf(false, false, false), rows.map { it.returnedToBoard })
    }

    @Test
    fun `rows come out oldest first and a row with no time sorts last`() {
        val rows = timelineRows(
            listOf(
                OrderStatusTrackDto(status = status(COMPLETED), createdOn = null),
                track(CONFIRMED, "11:00"),
                track(NEW, "10:00"),
            ),
        )

        assertEquals(listOf(NEW, CONFIRMED, COMPLETED), rows.map { it.track.status?.value })
    }

    @Test
    fun `an empty history has no rows`() {
        assertEquals(emptyList<TimelineEntry>(), timelineRows(emptyList()))
    }

    @Test
    fun `the walk-back label is translated in all five locales`() {
        val english = valueOf("values", WALK_BACK_KEY)
        assertNotNull("values/strings.xml is missing $WALK_BACK_KEY", english)
        listOf("values", "values-cs", "values-sk", "values-uk", "values-ru").forEach { locale ->
            val value = valueOf(locale, WALK_BACK_KEY)
            assertNotNull("$locale/strings.xml is missing $WALK_BACK_KEY", value)
            assertTrue("$locale/strings.xml has a blank $WALK_BACK_KEY", value!!.isNotBlank())
            if (locale != "values") assertTrue("$locale/strings.xml left $WALK_BACK_KEY in English", value != english)
        }
    }

    @Test
    fun `the card draws the rows the resolver returns and labels the walk-back`() {
        val card = File(resDir.parentFile, "java/cz/cleansia/customer/features/orders/OrderDetailTimelineAndReview.kt")
        assertTrue("OrderDetailTimelineAndReview.kt not found next to res/", card.isFile)
        val source = card.readText()

        assertTrue("TimelineCard no longer draws through timelineRows()", source.contains("timelineRows(history)"))
        assertTrue("the walk-back row no longer takes its own label", source.contains("R.string.$WALK_BACK_KEY"))
    }

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    private fun valueOf(locale: String, key: String): String? {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(file.readText())
            ?.groupValues
            ?.get(1)
    }

    private fun status(value: Int) = CodeDto(type = "OrderStatus", name = "status-$value", value = value)

    private fun track(value: Int, time: String) =
        OrderStatusTrackDto(status = status(value), createdOn = "2026-10-20T$time:00Z")

    private companion object {
        const val WALK_BACK_KEY = "order_detail_timeline_new_cleaner"
        const val NEW = 0
        const val CONFIRMED = 2
        const val IN_PROGRESS = 4
        const val COMPLETED = 5
    }
}
