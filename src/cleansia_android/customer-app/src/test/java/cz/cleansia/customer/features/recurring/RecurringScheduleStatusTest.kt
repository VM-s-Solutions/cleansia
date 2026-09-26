package cz.cleansia.customer.features.recurring

import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A cash schedule that now needs more than one cleaner is skipped by the server, not switched to card,
 * so the list must stop presenting it as running and say how to fix it.
 */
class RecurringScheduleStatusTest {

    private val template = RecurringBookingTemplateDto(
        id = "tpl-1",
        frequency = 1,
        dayOfWeek = 4,
        timeOfDay = "10:00",
        rooms = 2,
        bathrooms = 1,
        savedAddressId = "addr-1",
        paymentType = 1,
        startsOn = "2026-07-01T00:00:00Z",
        isActive = true,
        requiresPaymentMethodChange = false,
    )

    @Test
    fun `an active schedule that needs nothing books cleanings`() {
        assertEquals(ScheduleStatus.Active, ScheduleStatus.of(template))
    }

    @Test
    fun `a schedule the server skips for its payment needs a change`() {
        assertEquals(
            ScheduleStatus.NeedsPaymentChange,
            ScheduleStatus.of(template.copy(requiresPaymentMethodChange = true)),
        )
    }

    @Test
    fun `a paused schedule reads as paused whatever its payment`() {
        assertEquals(
            ScheduleStatus.Paused,
            ScheduleStatus.of(template.copy(isActive = false, requiresPaymentMethodChange = true)),
        )
    }

    @Test
    fun `the card states the change and offers the fix only to a customer who may edit`() {
        val source = File(moduleDir(), "src/main/java/cz/cleansia/customer/features/recurring/RecurringBookingsScreen.kt")
            .readText()
        val card = source.substringAfter("private fun TemplateCard(").substringBefore("private fun CardAction(")

        assertTrue("the card stopped reading the server's flag", card.contains("template.requiresPaymentMethodChange"))
        assertTrue(card.contains("R.string.recurring_cash_change_title"))
        assertTrue(card.contains("R.string.recurring_cash_change_body"))
        assertTrue(card.contains("R.string.recurring_status_needs_change"))
        val action = card.indexOf("R.string.recurring_cash_change_action")
        assertTrue("the card lost the way to change the schedule", action >= 0)
        assertTrue(
            "a lapsed member cannot edit, so the fix must follow showEdit",
            card.lastIndexOf("if (showEdit)", action) >= 0,
        )
    }

    /** Home lists unpaused schedules; one the server skips must not read there as running. */
    @Test
    fun `the home row marks a schedule that needs a change`() {
        val source = File(moduleDir(), "src/main/java/cz/cleansia/customer/features/home/HomeTab.kt").readText()
        val row = source.substringAfter("private fun RecurringScheduleRow(").substringBefore("\n@Composable")

        assertTrue("the home row stopped reading the schedule's status", row.contains("ScheduleStatus.of(template)"))
        assertTrue("the home row lost its needs-a-change badge", row.contains("R.string.recurring_status_needs_change"))
    }

    private fun moduleDir(): File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")
}
