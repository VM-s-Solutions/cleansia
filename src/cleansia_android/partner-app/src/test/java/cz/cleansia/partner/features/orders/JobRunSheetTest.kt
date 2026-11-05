package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The run sheet of her own job, on the mock's dataset: Thursday 12 November 2026 in Prague, A1
 * 14:00–16:00 solo, A2 Friday 15:00–18:00 with a crew-mate, A3 Monday 08:00–10:00. The current step is the status
 * line's: Cleaning closes at the first after photo solo and at her own mark on a crew, never a crew-mate's.
 */
class JobRunSheetTest {

    private val prague: ZoneId = ZoneId.of("Europe/Prague")
    private val en = Locale.ENGLISH

    private fun at(day: Int, hm: String): Instant = LocalDate.of(2026, 11, day).atTime(LocalTime.parse(hm)).atZone(prague).toInstant()

    private fun a1(status: Int, now: Instant, onWay: String? = null, started: String? = null, after: String? = null, finished: String? = null) =
        runSheet(
            status = status, start = at(12, "14:00"), minutes = 120, now = now, zone = prague, locale = en,
            onWayAt = onWay?.let { at(12, it) }, startedAt = started?.let { at(12, it) },
            firstAfterPhotoAt = after?.let { at(12, it) }, finishedAt = finished?.let { at(12, it) },
        )

    private fun a2(now: Instant, myMark: String? = null) = runSheet(
        status = 4, start = at(13, "15:00"), minutes = 180, now = now, zone = prague, locale = en, crew = true,
        onWayAt = at(13, "14:12"), startedAt = at(13, "15:02"), myMarkAt = myMark?.let { at(13, it) }, firstAfterPhotoAt = at(13, "17:38"),
    )

    private fun res(id: Int, vararg args: Any) = BoardText.Res(id, args.toList())
    private fun lit(text: String) = BoardText.Literal(text)
    private fun done(at: String) = res(R.string.run_step_done_at, at)

    private fun marks(rows: List<RunRow>) = rows.map { it.step to it.mark }

    @Test
    fun `Confirmed today, setting off is current from start - 60, the rest says when it comes`() {
        val rows = a1(2, at(12, "09:20"))
        assertEquals(
            listOf(RunStep.SetOff to RunMark.Current, RunStep.GetIn to RunMark.ToCome, RunStep.Clean to RunMark.ToCome, RunStep.Finish to RunMark.ToCome),
            marks(rows),
        )
        assertEquals(
            listOf(res(R.string.run_from, "13:00"), res(R.string.run_at, "14:00"), lit("14:00–16:00"), res(R.string.run_by, "16:00")),
            rows.map { it.at },
        )
    }

    @Test
    fun `another day names the weekday of the set-off`() {
        val rows = runSheet(status = 2, start = at(16, "08:00"), minutes = 120, now = at(12, "09:20"), zone = prague, locale = en)
        assertEquals(res(R.string.run_day_from, "Mon", "07:00"), rows.first().at)
    }

    @Test
    fun `late, not set off, setting off stays current and says it was due`() {
        val rows = a1(2, at(12, "14:10"))
        assertEquals(RunMark.Current, rows[0].mark)
        assertEquals(res(R.string.active_hero_due, "14:00"), rows[0].at)
    }

    @Test
    fun `on the way, setting off is done at its stamp, getting in is current`() {
        val rows = a1(3, at(12, "13:40"), onWay = "13:22")
        assertEquals(RunStep.SetOff to RunMark.Done, rows[0].step to rows[0].mark)
        assertEquals(lit("13:22"), rows[0].at)
        assertEquals(done("13:22"), rows[0].spoken)
        assertEquals(RunMark.Current, rows[1].mark)
        assertEquals(res(R.string.run_at, "14:00"), rows[1].at)
    }

    @Test
    fun `in progress with no after photo, cleaning is current, finishing to come`() {
        val rows = a1(4, at(12, "15:12"), onWay = "13:22", started = "13:58")
        assertEquals(listOf(RunMark.Done, RunMark.Done, RunMark.Current, RunMark.ToCome), rows.map { it.mark })
        assertEquals(listOf(lit("13:22"), lit("13:58"), lit("14:00–16:00"), res(R.string.run_by, "16:00")), rows.map { it.at })
    }

    @Test
    fun `solo, the first after photo closes cleaning and finishing is current`() {
        val rows = a1(4, at(12, "15:49"), onWay = "13:22", started = "13:58", after = "15:46")
        assertEquals(listOf(RunMark.Done, RunMark.Done, RunMark.Done, RunMark.Current), rows.map { it.mark })
        assertEquals(lit("15:46"), rows[2].at)
    }

    @Test
    fun `crew, the order's set-off and start stamps read as the job's, a crew-mate's mark never closes her cleaning`() {
        val rows = a2(at(13, "17:46"))
        assertEquals(res(R.string.run_job_stamp, "14:12"), rows[0].at)
        assertEquals(res(R.string.run_job_stamp_a11y, "14:12"), rows[0].spoken)
        assertEquals(res(R.string.run_job_stamp, "15:02"), rows[1].at)
        assertEquals(RunStep.Clean to RunMark.Current, rows[2].step to rows[2].mark)
        assertEquals(RunMark.ToCome, rows[3].mark)
    }

    @Test
    fun `crew, her own mark closes cleaning at her own stamp`() {
        val rows = a2(at(13, "17:46"), myMark = "17:44")
        assertEquals(RunStep.Clean to RunMark.Done, rows[2].step to rows[2].mark)
        assertEquals(lit("17:44"), rows[2].at)
        assertEquals(done("17:44"), rows[2].spoken)
        assertEquals(RunMark.Current, rows[3].mark)
    }

    @Test
    fun `completed, every step done at its stamp`() {
        val rows = a1(5, at(12, "16:20"), onWay = "13:22", started = "13:58", after = "15:46", finished = "15:51")
        assertTrue(rows.all { it.mark == RunMark.Done })
        assertEquals(listOf(lit("13:22"), lit("13:58"), lit("15:46"), lit("15:51")), rows.map { it.at })
    }

    @Test
    fun `a past crew job takes her own first after photo for cleaning, or the check alone`() {
        val past = { after: Instant? ->
            runSheet(
                status = 5, start = at(10, "09:00"), minutes = 120, now = at(12, "09:20"), zone = prague, locale = en, crew = true,
                firstAfterPhotoAt = after, finishedAt = at(10, "10:52"),
            )
        }
        assertEquals(lit("10:44"), past(at(10, "10:44"))[2].at)
        assertEquals(null, past(null)[2].at)
        assertEquals(RunMark.Done, past(null)[2].mark)
    }

    @Test
    fun `a cancelled job has no run sheet`() {
        assertTrue(a1(6, at(12, "09:20")).isEmpty())
    }

    @Test
    fun `the server's after photo closes Cleaning with the check alone until the photo list gives its time`() {
        val rows = runSheet(
            status = 4, start = at(12, "14:00"), minutes = 120, now = at(12, "15:49"), zone = prague, locale = en,
            onWayAt = at(12, "13:22"), startedAt = at(12, "13:58"), afterPhoto = true,
        )
        assertEquals(RunStep.Clean to RunMark.Done, marks(rows)[2])
        assertNull(rows[2].at)
        assertEquals(RunStep.Finish to RunMark.Current, marks(rows)[3])
    }
}
