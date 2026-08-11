package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderIssueDto
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderNoteDto
import cz.cleansia.partner.api.model.OrderStatus
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Every case here is built at the shape the two predicates disagree on — the field **populated** and
 * `isAssignedToCurrentUser` **false** — because that is the only caller class the old gate was wrong
 * for. An employee who books a cleaning for their own home reaches the detail as the order's
 * customer: `CanAccessOrderAsync` is true so the server redacts nothing, while the assignment flag is
 * false and the client hid that person's own data from them.
 *
 * A case with the flag true proves nothing, and a case with the field blank proves the server's
 * behaviour rather than the client's.
 */
class OrderDisclosurePresentationTest {

    private val entitledNonAssignee = OrderItem(
        isAssignedToCurrentUser = false,
        customerPhone = "+420600123456",
        accessInstructions = "Gate code 4417, second door on the left",
        orderNotes = listOf(OrderNoteDto(id = "n-1", content = "Cat is friendly")),
        orderIssues = listOf(OrderIssueDto(id = "i-1", description = "Vacuum belt snapped")),
    )

    /**
     * What `OrderPiiRedaction.RedactForBrowsingCleaner` actually sends, in all three forms it uses:
     * `CustomerPhone` is blanked to `string.Empty`, `AccessInstructions` is set to `null`, and the
     * two work-record lists become `[]`. A fixture that blanked the door code too would leave the
     * null arm untested on the one field the server nulls.
     */
    private val browsingCleaner = OrderItem(
        isAssignedToCurrentUser = false,
        customerPhone = "",
        accessInstructions = null,
        orderNotes = emptyList(),
        orderIssues = emptyList(),
    )

    @Test
    fun `the entitled non-assignee gets the phone the server sent them`() {
        val disclosure = entitledNonAssignee.orderDisclosure()

        assertTrue(disclosure.showsCustomerContact)
        assertEquals("+420600123456", disclosure.customerPhone)
    }

    @Test
    fun `the entitled non-assignee gets the door code the server sent them`() {
        val disclosure = entitledNonAssignee.orderDisclosure()

        assertTrue(disclosure.showsAccessInstructions)
        assertEquals("Gate code 4417, second door on the left", disclosure.accessInstructions)
    }

    @Test
    fun `the entitled non-assignee gets the notes and issues the server sent them`() {
        assertTrue(entitledNonAssignee.orderDisclosure().showsWorkRecord)
    }

    @Test
    fun `a browsing cleaner gets nothing, because the server sent nothing`() {
        val disclosure = browsingCleaner.orderDisclosure()

        assertFalse(disclosure.showsCustomerContact)
        assertFalse(disclosure.showsAccessInstructions)
        assertFalse(disclosure.showsWorkRecord)
        assertNull(disclosure.customerPhone)
        assertNull(disclosure.accessInstructions)
    }

    /** The redaction blanks; it does not null. A `!= null` test would disclose an empty phone chip. */
    @Test
    fun `whitespace counts as absent, the same as blank`() {
        val padded = OrderItem(customerPhone = "   ", accessInstructions = "\n")

        assertFalse(padded.orderDisclosure().showsCustomerContact)
        assertFalse(padded.orderDisclosure().showsAccessInstructions)
    }

    @Test
    fun `an absent field is absent, not an empty string`() {
        val nothing = OrderItem()

        assertFalse(nothing.orderDisclosure().showsCustomerContact)
        assertFalse(nothing.orderDisclosure().showsAccessInstructions)
        assertFalse(nothing.orderDisclosure().showsWorkRecord)
    }

    /** Only one of the two lists needs to arrive for the record to be worth rendering. */
    @Test
    fun `issues alone are a work record`() {
        val issuesOnly = OrderItem(
            isAssignedToCurrentUser = false,
            orderNotes = emptyList(),
            orderIssues = listOf(OrderIssueDto(id = "i-1", description = "Vacuum belt snapped")),
        )

        assertTrue(issuesOnly.orderDisclosure().showsWorkRecord)
    }

    @Test
    fun `the entitled non-assignee gets the access card on the way and on the job`() {
        val disclosure = entitledNonAssignee.orderDisclosure()

        assertTrue(disclosure.showsAccessCard(OrderStatus._3))
        assertTrue(disclosure.showsAccessCard(OrderStatus._4))
    }

    /** The lifecycle conjunct is the one term that must survive the migration. */
    @Test
    fun `the access card still hides outside the window it is useful in`() {
        val disclosure = entitledNonAssignee.orderDisclosure()

        assertFalse("a door code on a Confirmed job is early", disclosure.showsAccessCard(OrderStatus._2))
        assertFalse("a door code on a finished job is permanent", disclosure.showsAccessCard(OrderStatus._5))
        assertFalse(disclosure.showsAccessCard(OrderStatus._6))
        assertFalse(disclosure.showsAccessCard(null))
    }

    @Test
    fun `a browsing cleaner gets no access card in any status`() {
        OrderStatus.entries.forEach { status ->
            assertFalse("$status", browsingCleaner.orderDisclosure().showsAccessCard(status))
        }
    }

    @Test
    fun `the entitled non-assignee reads the work record without being able to add to it`() {
        assertTrue(entitledNonAssignee.orderDisclosure().showsWorkRecordSection(canAddNotesOrIssues = false))
    }

    /** The assignee's first note has to be addable from somewhere, and this section is that somewhere. */
    @Test
    fun `an assignee with an empty record still gets the section to add from`() {
        assertTrue(browsingCleaner.orderDisclosure().showsWorkRecordSection(canAddNotesOrIssues = true))
    }

    @Test
    fun `nothing to read and nothing to add draws no section`() {
        assertFalse(browsingCleaner.orderDisclosure().showsWorkRecordSection(canAddNotesOrIssues = false))
    }

    /**
     * The resolver takes the order and nothing else — there is no flag to pass it, which is the
     * structural half of the rule: a second authorization implementation cannot be written here.
     */
    @Test
    fun `the assignment flag changes nothing about what is disclosed`() {
        val assigned = entitledNonAssignee.copy(isAssignedToCurrentUser = true)

        assertEquals(entitledNonAssignee.orderDisclosure(), assigned.orderDisclosure())
    }
}
