package cz.cleansia.partner.features.orders

import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.AssignedEmployeeDto
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderListItem
import java.io.File
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The job detail's pure rules on the mock's dataset, Thursday 12 November 2026 09:20 in Prague: the
 * cancelled pay line, the window bar, a job not taken naming its day, the removal page's job line and lists, the crew
 * by given name, the access pieces, when the customer's details close, where the lockout sits, the collapsed panel's
 * height, and the detail's strings.
 */
class JobDetailRulesTest {

    private val rules = OrdersBoardRules
    private val prague: ZoneId = ZoneId.of("Europe/Prague")
    private val en = Locale.ENGLISH

    private fun at(day: Int, hm: String): Instant = LocalDate.of(2026, 11, day).atTime(LocalTime.parse(hm)).atZone(prague).toInstant()

    private val now = at(12, "09:20")

    @Test
    fun `the collapsed panel ends clean under its head, above the bar`() {
        assertEquals("the handle and head, and the head's edge", 204.dp, collapsedHeight(196.dp))
        assertNull("unmeasured: the sheet's own share", collapsedHeight(0.dp))
    }

    private fun row(id: String, day: Int, from: String, minutes: Int, street: String? = null) = OrderListItem(
        id = id,
        cleaningDateTime = at(day, from).toString(),
        estimatedTime = minutes,
        customerAddress = street?.let { "$it, Praha, 13000" },
    )

    private fun res(id: Int, vararg args: Any) = BoardText.Res(id, args.toList())

    @Test
    fun `a cancel with no system reason keeps the general fee rule`() {
        assertEquals(R.string.work_contract_summary_late_cancel, rules.cancelPayLine(null))
        assertEquals(R.string.work_contract_summary_late_cancel, rules.cancelPayLine("order.cancelled.something_new"))
    }

    @Test
    fun `a confirmed lockout pays the full reward and no fee rule, the other four reasons pay nothing`() {
        assertEquals(R.string.cancel_lockout_paid, rules.cancelPayLine("order.cancelled.customer_lockout"))
        listOf("recurring_not_confirmed", "payment_not_completed", "no_cleaner_available", "company_wind_down").forEach {
            assertEquals(it, R.string.cancel_no_fee, rules.cancelPayLine("order.cancelled.$it"))
        }
    }

    @Test
    fun `the window bar fills by the booked window and stops at its ends`() {
        assertEquals(0.6f, rules.windowRatio(at(12, "14:00"), 120, at(12, "15:12")), 0.0001f)
        assertEquals(0f, rules.windowRatio(at(12, "14:00"), 120, at(12, "13:40")), 0f)
        assertEquals(1f, rules.windowRatio(at(12, "14:00"), 120, at(12, "16:20")), 0f)
        assertEquals(0f, rules.windowRatio(at(12, "14:00"), 0, at(12, "15:00")), 0f)
    }

    @Test
    fun `the status line says the minutes left to the booked end, then booked until`() {
        val line = { t: Instant -> rules.runStatus(status = 4, start = at(12, "14:00"), minutes = 120, now = t, zone = prague, locale = en) }
        assertEquals(res(R.string.detail_left, rules.durationText(48)), line(at(12, "15:12"))[1])
        assertEquals(res(R.string.active_hero_until, "16:00"), line(at(12, "16:05"))[1])
    }

    @Test
    fun `the late joiner names one crew-mate by given name, else the board's count`() {
        fun job(vararg names: String?) = OrderItem(
            assignedEmployeesCount = names.size,
            assignedEmployees = names.mapIndexed { i, name -> AssignedEmployeeDto(id = "s$i", employeeId = "e$i", fullName = name) },
        )
        assertEquals(res(R.string.detail_mate_on_it, "Marta"), job("Marta Nová").lateJoinCrew())
        assertEquals(BoardText.Plural(R.plurals.orders_flag_crew_taken, 2), job("Marta Nová", "Eva Malá").lateJoinCrew())
        assertEquals(BoardText.Plural(R.plurals.orders_flag_crew_taken, 1), job(" ").lateJoinCrew())
        assertNull(OrderItem(assignedEmployeesCount = 0, assignedEmployees = emptyList()).lateJoinCrew())
    }

    @Test
    fun `the removal page names the job from the store's row`() {
        assertEquals(
            listOf(res(R.string.day_tomorrow), BoardText.Literal("15:00–18:00"), BoardText.Literal("Seifertova 21")),
            rules.removalJobLine(row("A2", 13, "15:00", 180, "Seifertova 21"), at(12, "10:05"), prague, en),
        )
        assertEquals(
            listOf(BoardText.Literal("Monday 16 Nov"), BoardText.Literal("08:00–10:00"), BoardText.Literal("Hornoměcholupská 12")),
            rules.removalJobLine(row("A3", 16, "08:00", 120, "Hornoměcholupská 12"), at(12, "10:05"), prague, en),
        )
    }

    @Test
    fun `her next jobs never hold this id, nor a job that has ended, soonest first`() {
        val own = listOf(row("A3", 16, "08:00", 120), row("A2", 13, "15:00", 180), row("A1", 12, "14:00", 120), row("A0", 12, "07:00", 120))
        assertEquals(listOf("A1", "A3"), rules.nextOwn(own, exceptId = "A2", now = now).map { it.id })
    }

    @Test
    fun `also open that day is the board's rows of that day still open, less this one`() {
        val available = listOf(row("V3", 13, "08:00", 120), row("V4", 13, "14:00", 120), row("V1", 12, "08:00", 60), row("V9", 17, "15:00", 120))
        assertEquals(listOf("V3"), rules.openOfDay(available, LocalDate.of(2026, 11, 13), "V4", now, prague).map { it.id })
        assertEquals(emptyList<String>(), rules.openOfDay(available, LocalDate.of(2026, 11, 12), null, now, prague).map { it.id })
    }

    @Test
    fun `the gate list names each seat by given name with its own mark, the crew-mates before her`() {
        val order = OrderItem(
            assignedEmployees = listOf(
                AssignedEmployeeDto(id = "s1", employeeId = "me", fullName = "Oksana Melnyk", phoneNumber = "+420 700 000 001", completionConfirmedAt = "2026-11-13T16:44:00Z"),
                AssignedEmployeeDto(id = "s2", employeeId = "mate", fullName = "Lenka Procházková", phoneNumber = "+420 605 318 772"),
            ),
        )
        assertEquals(
            listOf(
                CrewSeat(givenName = "Lenka", initials = "LP", doneAt = null, phone = "+420 605 318 772", isMe = false),
                CrewSeat(givenName = "Oksana", initials = "OM", doneAt = Instant.parse("2026-11-13T16:44:00Z"), phone = "+420 700 000 001", isMe = true),
            ),
            order.crewSeats("me"),
        )
    }

    @Test
    fun `a crew-mate is called only while her part is open, never on her own seat, a marked seat or a blank phone`() {
        val marked = Instant.parse("2026-11-13T16:41:00Z")
        val mate = CrewSeat(givenName = "Lenka", initials = "LP", doneAt = null, phone = "+420 605 318 772", isMe = false)
        assertEquals("+420 605 318 772", mate.callNumber())
        assertNull(mate.copy(isMe = true).callNumber())
        assertNull(mate.copy(doneAt = marked).callNumber())
        assertNull(mate.copy(phone = null).callNumber())
        assertNull(mate.copy(phone = " ").callNumber())
    }

    @Test
    fun `a blank name or phone is left out, never shown as a dash`() {
        val order = OrderItem(assignedEmployees = listOf(AssignedEmployeeDto(id = "s2", employeeId = "mate", fullName = " ", phoneNumber = "")))
        assertEquals(listOf(CrewSeat(givenName = null, initials = "", doneAt = null, phone = null, isMe = false)), order.crewSeats("me"))
    }

    private fun access(mode: String?, floor: String?, flat: String?) = OrderItem(accessMode = mode, customerFloor = floor, customerApartment = flat)

    @Test
    fun `mode, floor and flat, each left out when it did not arrive`() {
        assertEquals(
            listOf(res(R.string.access_mode_at_home), res(R.string.detail_floor, "3"), res(R.string.detail_flat, "12")),
            access("at_home", "3", "12").accessFacts(),
        )
        assertEquals(listOf(res(R.string.detail_floor, "3"), res(R.string.detail_flat, "12")), access(null, "3", "12").accessFacts())
        assertEquals(listOf(res(R.string.detail_floor, "3")), access(null, "3", null).accessFacts())
        assertEquals(listOf(res(R.string.access_mode_door_code)), access("door_code", " ", null).accessFacts())
        assertTrue(access(null, null, null).accessFacts().isEmpty())
        assertTrue(access("teleport", null, null).accessFacts().isEmpty())
    }

    @Test
    fun `each access mode the wizard writes has its words`() {
        assertEquals(R.string.access_mode_at_home, accessModeRes("at_home"))
        assertEquals(R.string.access_mode_keys_handover, accessModeRes("keys_handover"))
        assertEquals(R.string.access_mode_door_code, accessModeRes("door_code"))
        assertEquals(R.string.access_mode_reception, accessModeRes("reception"))
        assertNull(accessModeRes(null))
    }

    @Test
    fun `the details close tomorrow on the job's day, today when read the next day`() {
        val completed = at(12, "15:51")
        assertEquals(R.string.details_close_tomorrow, detailsCloseRes(completed, at(12, "16:20"), prague))
        assertEquals(R.string.details_close_tomorrow, detailsCloseRes(completed, at(12, "23:59"), prague))
        assertEquals(R.string.details_close_today, detailsCloseRes(completed, at(13, "00:01"), prague))
        assertEquals(R.string.details_close_today, detailsCloseRes(completed, at(13, "09:00"), prague))
    }

    private fun mine(status: Int) = OrderItem(orderStatus = Code(value = status), isAssignedToCurrentUser = true, cleaningDateTime = at(12, "14:00").toString())

    @Test
    fun `the lockout shows on the way, on Confirmed only once the start has passed, never once cleaning started`() {
        assertFalse(mine(2).showsLockout(at(12, "09:20")))
        assertFalse(mine(2).showsLockout(at(12, "13:59")))
        assertTrue(mine(2).showsLockout(at(12, "14:10")))
        assertTrue(mine(3).showsLockout(at(12, "13:40")))
        assertFalse(mine(4).showsLockout(at(12, "15:12")))
        assertFalse(mine(3).copy(isAssignedToCurrentUser = false).showsLockout(at(12, "13:40")))
    }

    private val resDir: File = sequenceOf(File("src/main/res"), File("partner-app/src/main/res"), File("src/cleansia_android/partner-app/src/main/res"))
        .firstOrNull { it.isDirectory } ?: error("partner-app res/ not found from ${File(".").absolutePath}")

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /** Every key the detail adds on Android, with the slots its sentence carries in every locale. */
    private val detailKeys = mapOf(
        "cancel_lockout_paid" to 0, "cancel_no_fee" to 0, "run_job_stamp" to 1, "run_job_stamp_a11y" to 1, "late_already_there" to 0,
        "crew_all_marked" to 0, "detail_waiting_for" to 1, "detail_your_job_that_day" to 0, "detail_mate_on_it" to 1, "detail_same_pay_late" to 0,
        "run_a11y" to 0, "run_step_done_at" to 1, "run_from" to 1, "run_day_from" to 2, "run_at" to 1, "run_by" to 1,
        "access_mode_at_home" to 0, "access_mode_keys_handover" to 0, "access_mode_door_code" to 0, "access_mode_reception" to 0,
        "detail_floor" to 1, "detail_flat" to 1, "access_shown_on_set_off" to 0, "lockout_paid_in_full" to 0, "checklist_phone_only" to 0,
        "photos_add" to 0, "photos_after_needed" to 0, "issue_not_resolved" to 0, "crew_you" to 0, "crew_not_yet" to 0, "crew_done_at" to 1,
        "finish_needs_photo" to 0, "finish_needs_crew" to 0, "finish_before_complete" to 0, "finish_after_photo" to 0, "finish_cash_collected" to 0,
        "crew_both_parts" to 0, "cash_rule" to 1, "cash_mark_amount" to 1, "crew_mark_done_sub" to 0,
        "take_after_photo" to 0, "drop_possible_until" to 1,
        "detail_shown_after_take" to 0, "detail_hidden_address" to 0, "detail_hidden_contact" to 0, "detail_hidden_notes" to 0,
        "details_close_tomorrow" to 1, "details_close_today" to 1, "day_line_free" to 0, "detail_later_today" to 0,
        "detail_also_open_that_day" to 0, "detail_open_today" to 0, "detail_error_rest" to 0, "detail_error_rest_body" to 0,
        "detail_error_job" to 0, "detail_gone" to 0, "detail_off_schedule" to 0, "order_removal_lead" to 0,
        "partner_order_complete_blocked_cash_sequence" to 0, "instructions_copy" to 0, "instructions_copied" to 0,
        "status_new" to 0, "status_confirmed" to 0, "status_in_progress" to 0, "status_completed" to 0, "status_cancelled" to 0,
        "scope_section_title" to 0, "scope_services_label" to 0, "scope_extras_label" to 0, "payment_section_title" to 0,
        "contract_accepted_on_label" to 0, "contract_version_label" to 0,
    )

    @Test
    fun `every detail string is written in all five locales with the slots its sentence needs`() {
        locales.forEach { locale ->
            val xml = File(resDir, "$locale/strings.xml").readText()
            detailKeys.forEach { (key, slots) ->
                val value = Regex("<string name=\"$key\">(.*?)</string>", RegexOption.DOT_MATCHES_ALL).find(xml)?.groupValues?.get(1)
                assertTrue("$locale/$key is missing or blank", !value.isNullOrBlank())
                val found = Regex("%(\\d)\\\$s").findAll(value!!).map { it.groupValues[1].toInt() }.toSortedSet()
                assertEquals("$locale/$key slots: $value", (1..slots).toSortedSet(), found)
            }
        }
    }

    /** The detail's old tracker, timer, timeline, map, payment card and alert copy, gone once nothing reads it. */
    @Test
    fun `the keys the detail no longer reads are gone from all five locales`() {
        val dropped = listOf(
            "tracker_countdown_starts_in", "tracker_subtitle_on_the_way_arriving", "tracker_completed_in", "notify_on_the_way",
            "status_timeline_section_title", "map_approximate_area", "dirtiness_level_normal",
            "payment_method_value", "payment_method_card", "checklist_all_done_hint", "work_contract_accepted_line",
            "tracker_headline_in_progress", "order_removal_message", "order_my_part_done_waiting",
        )
        locales.forEach { locale ->
            val xml = File(resDir, "$locale/strings.xml").readText()
            dropped.forEach { assertFalse("$locale still declares $it", xml.contains("name=\"$it\"")) }
        }
    }

    @Test
    fun `withdraw says withdraw in English, the dialog included, and the other locales are untouched`() {
        val english = File(resDir, "values/strings.xml").readText()
        mapOf(
            "order_drop_action" to "Withdraw from this job",
            "order_drop_confirm_title" to "Withdraw from this job?",
            "order_drop_confirm_action" to "Yes, withdraw",
            "take_order" to "Take this job",
        ).forEach { (key, value) ->
            assertEquals(key, value, Regex("<string name=\"$key\">(.*?)</string>").find(english)?.groupValues?.get(1))
        }
        val czech = File(resDir, "values-cs/strings.xml").readText()
        assertEquals("Odstoupit od této zakázky?", Regex("<string name=\"order_drop_confirm_title\">(.*?)</string>").find(czech)?.groupValues?.get(1))
    }
}
