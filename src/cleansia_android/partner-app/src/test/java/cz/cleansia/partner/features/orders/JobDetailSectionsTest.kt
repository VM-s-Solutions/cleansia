package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import java.io.File
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * What the job detail says where, on the mock's dataset (Thursday 12 November 2026 in Prague; A1 14:00–16:00 solo, A2
 * Friday 15:00–18:00 with a crew-mate). Each fact once: the head counts the run ("Step n / 4") and fills its segment,
 * the reference sections each hold only what no step says, and the crew job's one Call is the pinned move.
 */
class JobDetailSectionsTest {

    private val prague: ZoneId = ZoneId.of("Europe/Prague")

    private fun at(day: Int, hm: String): Instant = LocalDate.of(2026, 11, day).atTime(LocalTime.parse(hm)).atZone(prague).toInstant()

    private val a1Start = at(12, "14:00")

    @Test
    fun `the head counts the step of four and fills the current segment as far as the step has run`() {
        assertEquals(HeadProgress(step = 1, currentIndex = 0, fraction = null), headProgress(RunStep.SetOff, a1Start, 120, at(12, "09:20")))
        assertEquals(HeadProgress(step = 1, currentIndex = 0, fraction = 1f / 6), headProgress(RunStep.SetOff, a1Start, 120, at(12, "13:10")))
        assertEquals(HeadProgress(step = 2, currentIndex = 1, fraction = null), headProgress(RunStep.GetIn, a1Start, 120, at(12, "13:40")))
        assertEquals(HeadProgress(step = 3, currentIndex = 2, fraction = 0.6f), headProgress(RunStep.Clean, a1Start, 120, at(12, "15:12")))
        assertEquals(HeadProgress(step = 4, currentIndex = 3, fraction = 1f), headProgress(RunStep.Finish, a1Start, 120, at(12, "16:30")))
        assertEquals(HeadProgress(step = 4, currentIndex = 4, fraction = null), headProgress(RunStep.Done, a1Start, 120, at(12, "16:20")))
        assertNull(headProgress(RunStep.Cancelled, a1Start, 120, at(12, "09:20")))
        assertEquals(4, RUN_STEPS)
    }

    @Test
    fun `the step counter says Step n of 4 in all five locales`() {
        listOf("values", "values-cs", "values-sk", "values-uk", "values-ru").forEach { locale ->
            val xml = File(resDir, "$locale/strings.xml").readText()
            val value = Regex("<string name=\"tracker_step_counter\">(.*?)</string>").find(xml)?.groupValues?.get(1)
            assertTrue("$locale/tracker_step_counter is missing", value != null)
            assertTrue("$locale/tracker_step_counter: $value", value!!.contains("%1\$d") && value.contains("%2\$d"))
        }
    }

    @Test
    fun `before cleaning the customer's words and the scope are sections, and her name is the Setting off row's`() {
        listOf(2 to RunStep.SetOff, 3 to RunStep.GetIn).forEach { (status, step) ->
            val s = ownSections(status = status, step = step, crew = false, mateMarked = false, cash = false, past = false, accessShown = true)
            assertEquals(OwnSections(who = CustomerWho.None, customerWords = true, access = false, scopeRows = true, cashRule = false, payouts = false), s)
        }
    }

    @Test
    fun `while the checklist is open it is the scope, and how to get in joins the Customer section`() {
        val cleaning = ownSections(status = 4, step = RunStep.Clean, crew = false, mateMarked = false, cash = false, past = false, accessShown = true)
        assertEquals(OwnSections(who = CustomerWho.None, customerWords = true, access = true, scopeRows = false, cashRule = false, payouts = false), cleaning)
        val ready = ownSections(status = 4, step = RunStep.Finish, crew = false, mateMarked = false, cash = false, past = false, accessShown = false)
        assertEquals(OwnSections(who = CustomerWho.None, customerWords = true, access = false, scopeRows = false, cashRule = false, payouts = false), ready)
    }

    @Test
    fun `a crew job's gates and the cash step leave the customer's words out and keep the scope rows`() {
        val crewWait = ownSections(status = 4, step = RunStep.Finish, crew = true, mateMarked = false, cash = false, past = false, accessShown = true)
        assertEquals(OwnSections(who = CustomerWho.None, customerWords = false, access = true, scopeRows = true, cashRule = false, payouts = false), crewWait)
        val mateMarked = ownSections(status = 4, step = RunStep.Clean, crew = true, mateMarked = true, cash = false, past = false, accessShown = true)
        assertFalse(mateMarked.customerWords)
        assertTrue(mateMarked.scopeRows)
        val mateOpen = ownSections(status = 4, step = RunStep.Clean, crew = true, mateMarked = false, cash = false, past = false, accessShown = true)
        assertTrue(mateOpen.customerWords)
        assertFalse(mateOpen.scopeRows)
        val cash = ownSections(status = 4, step = RunStep.Finish, crew = false, mateMarked = false, cash = true, past = false, accessShown = true)
        assertEquals(OwnSections(who = CustomerWho.None, customerWords = false, access = true, scopeRows = true, cashRule = true, payouts = false), cash)
    }

    @Test
    fun `once completed the customer is the section's, Payment says where the payout goes, and a past job's details are removed`() {
        val completed = ownSections(status = 5, step = RunStep.Done, crew = false, mateMarked = false, cash = false, past = false, accessShown = false)
        assertEquals(OwnSections(who = CustomerWho.Contact, customerWords = false, access = false, scopeRows = true, cashRule = false, payouts = true), completed)
        val past = ownSections(status = 5, step = RunStep.Done, crew = false, mateMarked = false, cash = true, past = true, accessShown = false)
        assertEquals(OwnSections(who = CustomerWho.Removed, customerWords = false, access = false, scopeRows = true, cashRule = true, payouts = true), past)
    }

    @Test
    fun `a card job before completion has no Payment section`() {
        val today = ownSections(status = 2, step = RunStep.SetOff, crew = false, mateMarked = false, cash = false, past = false, accessShown = false)
        assertFalse(today.cashRule || today.payouts)
    }

    /** A job she has not taken says how far off it is; its weekday is the calendar line's. */
    @Test
    fun `a job not taken names how far off it is, never its weekday`() {
        assertEquals(BoardText.Res(R.string.day_today), notTakenDay(at(12, "18:00"), at(12, "09:20"), prague))
        assertEquals(BoardText.Res(R.string.day_tomorrow), notTakenDay(at(13, "14:00"), at(12, "09:20"), prague))
        assertEquals(BoardText.Plural(R.plurals.detail_in_days, 2), notTakenDay(at(14, "10:00"), at(12, "23:50"), prague))
    }

    private val resDir: File = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/res") }
        .firstOrNull { File(it, "values/strings.xml").isFile }
        ?: error("partner-app res/ not found from ${File(".").absolutePath}")
}
