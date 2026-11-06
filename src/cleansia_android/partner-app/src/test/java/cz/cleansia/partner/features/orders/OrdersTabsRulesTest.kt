package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.CurrencyListItem
import cz.cleansia.partner.api.model.OrderListItem
import java.io.File
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The tabs' own decisions: which half of the day line a short mark takes, the History
 * total as one string and where the unpaid-cash line may break.
 */
class OrdersTabsRulesTest {

    private val rules = OrdersBoardRules
    private val nb = ' '

    @Test
    fun `the switcher's three segments share the track equally while each fits its share`() {
        assertEquals(listOf(100, 100, 100), segmentWidths(listOf(80, 70, 60), 300))
        assertEquals(listOf(100, 100, 101), segmentWidths(listOf(10, 10, 10), 301))
    }

    @Test
    fun `a segment that needs more than its share takes the room from the others, which keep their own need`() {
        assertEquals(listOf(114, 104, 104), segmentWidths(listOf(114, 96, 76), 322))
        assertEquals(listOf(150, 100, 98), segmentWidths(listOf(150, 100, 60), 348))
    }

    @Test
    fun `with no room for every need, each segment takes its own`() {
        assertEquals(listOf(150, 120, 100), segmentWidths(listOf(150, 120, 100), 300))
    }

    /** 07:00–19:00 on a 360 px track: half a pixel a minute, so a 7-minute job is drawn at the 12 px least width. */
    private val range = DayRange(7 * 60, 19 * 60)
    private fun mark(from: String, to: String, mine: Boolean = false, lane: SlotLane = SlotLane.Full) =
        DayMark(mine = mine, span = DaySpan(minutes(from), minutes(to)), lane = lane)
    private fun minutes(hm: String) = hm.substringBefore(':').toInt() * 60 + hm.substringAfter(':').toInt()
    private fun lanes(vararg marks: DayMark) = rules.dayLineLanes(marks.toList(), range, trackPx = 360f, minPx = 12f, insetPx = 1f)

    @Test
    fun `two short open jobs whose least-width marks touch take the upper and lower half, the earlier on top`() {
        assertEquals(listOf(SlotLane.Top, SlotLane.Bottom), lanes(mark("12:15", "12:22"), mark("12:30", "12:37")))
        assertEquals(listOf(SlotLane.Bottom, SlotLane.Top), lanes(mark("12:30", "12:37"), mark("12:15", "12:22")))
    }

    @Test
    fun `her own job keeps the upper half beside an open job drawn against it`() {
        assertEquals(listOf(SlotLane.Bottom, SlotLane.Top), lanes(mark("16:05", "16:12"), mark("14:00", "16:00", mine = true)))
    }

    @Test
    fun `an open job that overlaps hers in time is the clash and both keep the full track`() {
        assertEquals(listOf(SlotLane.Full, SlotLane.Full), lanes(mark("15:00", "18:00", mine = true), mark("14:00", "16:00")))
    }

    @Test
    fun `marks apart keep the full track, and two overlapping open jobs keep the lanes the board gave them`() {
        assertEquals(listOf(SlotLane.Full, SlotLane.Full), lanes(mark("08:00", "10:00"), mark("14:00", "16:00")))
        assertEquals(
            listOf(SlotLane.Top, SlotLane.Bottom),
            lanes(mark("09:00", "12:30", lane = SlotLane.Top), mark("10:00", "12:00", lane = SlotLane.Bottom)),
        )
    }

    @Test
    fun `her short job drawn against a mark the board laned keeps the board's lanes and takes the free half`() {
        assertEquals(
            listOf(SlotLane.Bottom, SlotLane.Top, SlotLane.Bottom),
            lanes(mark("12:31", "12:40", mine = true), mark("09:00", "12:30", lane = SlotLane.Top), mark("10:00", "12:00", lane = SlotLane.Bottom)),
        )
        assertEquals(
            listOf(SlotLane.Top, SlotLane.Bottom, SlotLane.Bottom),
            lanes(mark("09:00", "12:30", lane = SlotLane.Top), mark("10:00", "12:00", lane = SlotLane.Bottom), mark("12:31", "12:40", mine = true)),
        )
    }

    private val czk = CurrencyListItem(id = "cur-czk", code = "CZK", symbol = "Kč")
    private fun done(pay: Double) = OrderListItem(id = "h$pay", estimatedCleanerPay = pay, estimatedTime = 120, orderStatus = Code(value = 5), currency = czk)

    @Test
    fun `the History total is one string that never breaks inside a figure`() {
        val total = rules.historyTotalText(listOf(done(5000.0), done(1506.0)), total = 2, fallbackSymbol = null, locale = Locale.US)
        assertEquals("6${nb}506${nb}Kč", total)
        assertEquals("0${nb}Kč", rules.historyTotalText(emptyList(), total = 0, fallbackSymbol = "Kč", locale = Locale.US))
    }

    @Test
    fun `a total in two currencies may break only between them`() {
        val eur = CurrencyListItem(id = "cur-eur", code = "EUR", symbol = "€")
        val rows = listOf(done(1250.0), done(1000.0), done(40.0).copy(id = "e", currency = eur))
        assertEquals("2${nb}250${nb}Kč · 40${nb}€", rules.historyTotalText(rows, total = 3, fallbackSymbol = null, locale = Locale.US))
    }

    private val resDir: File = sequenceOf(File("src/main/res"), File("partner-app/src/main/res"), File("src/cleansia_android/partner-app/src/main/res"))
        .firstOrNull { it.isDirectory } ?: error("partner-app res/ not found from ${File(".").absolutePath}")

    private fun string(dir: String, name: String): String =
        Regex("""<string name="$name">(.*?)</string>""").find(File(resDir, "$dir/strings.xml").readText())?.groupValues?.get(1)
            ?.replace("\\u00A0", "$nb")
            ?: error("$name missing in $dir")

    @Test
    fun `the unpaid-cash line holds its verb together and lets the amount take its own line, never breaking inside it`() {
        val shipped = mapOf(
            "values" to ("Cash: the customer did not pay %1\$s" to "did not pay"),
            "values-cs" to ("Hotově: zákazník nezaplatil %1\$s" to "nezaplatil"),
            "values-sk" to ("V hotovosti: zákazník nezaplatil %1\$s" to "nezaplatil"),
            "values-uk" to ("Готівка: клієнт не заплатив %1\$s" to "не заплатив"),
            "values-ru" to ("Наличные: клиент не заплатил %1\$s" to "не заплатил"),
        )
        shipped.forEach { (dir, expected) ->
            val (words, verb) = expected
            val text = string(dir, "history_cash_not_paid")
            assertEquals("$dir: the words do not change", words, text.replace(nb, ' '))
            val held = text.substring(text.indexOf(verb.substringBefore(' ')), text.indexOf("%1\$s"))
            assertFalse("$dir: a line may break inside `$held`", held.trimEnd().contains(' '))
            assertTrue("$dir: the amount may start a line of its own", held.endsWith(" "))
        }
    }

    @Test
    fun `a ledger window holds its dash, so it breaks before or after the whole window only`() {
        assertEquals("09:00\u2060–\u206013:00", rules.holdDash("09:00–13:00"))
        assertEquals("09:00", rules.holdDash("09:00"))
    }
}
