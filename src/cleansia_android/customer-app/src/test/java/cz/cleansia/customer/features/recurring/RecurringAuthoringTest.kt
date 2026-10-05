package cz.cleansia.customer.features.recurring

import cz.cleansia.customer.R
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipStatus
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The server gates authoring (`CreateRecurringBooking`, `UpdateRecurringBooking`)
 * on an active membership and deliberately leaves pause, resume and delete open,
 * so a lapsed subscriber can always stop a schedule that is still generating
 * billable cleanings. These pin the client to the same split.
 */
class RecurringAuthoringTest {

    @Test
    fun `a resolved non-member is refused authoring`() {
        assertEquals(RecurringAuthoringGate.Upsell, RecurringAuthoringGate.resolve(member(false)))
    }

    @Test
    fun `a member may author`() {
        assertEquals(RecurringAuthoringGate.Allowed, RecurringAuthoringGate.resolve(member(true, MembershipStatus.Active)))
    }

    /**
     * The server authors and books schedules only on an Active membership, while
     * `GetMyMembership` counts a past-due or paused enrolment as a membership.
     */
    @Test
    fun `a member whose renewal failed or was paused is refused authoring without the upsell`() {
        listOf(MembershipStatus.PastDue, MembershipStatus.Paused).forEach { status ->
            assertEquals(status.name, RecurringAuthoringGate.Paused, RecurringAuthoringGate.resolve(member(true, status)))
        }
    }

    @Test
    fun `a paused member keeps their schedules with a notice and no create, edit or subscribe affordance`() {
        listOf(true, false).forEach { hasTemplates ->
            val affordances = RecurringListAffordances.of(RecurringAuthoringGate.Paused, hasTemplates)

            assertTrue("hasTemplates=$hasTemplates", affordances.showPausedNotice)
            assertFalse("hasTemplates=$hasTemplates", affordances.showCreateAction)
            assertFalse("hasTemplates=$hasTemplates", affordances.showEdit)
            assertFalse("hasTemplates=$hasTemplates", affordances.showPlusUpsell)
            assertFalse("hasTemplates=$hasTemplates", affordances.showLapsedNotice)
        }
    }

    /**
     * The reported defect: the screen read a cache another screen populated, so on
     * cold entry a fully paid-up member met the upsell wall. Unknown must resolve
     * the permissive way — the server refuses an unentitled create anyway.
     */
    @Test
    fun `an unresolved membership fails open`() {
        assertEquals(RecurringAuthoringGate.Allowed, RecurringAuthoringGate.resolve(null))
    }

    @Test
    fun `a non-member with schedules gets the lapsed notice and no create affordance`() {
        val affordances = RecurringListAffordances.of(RecurringAuthoringGate.Upsell, hasTemplates = true)

        assertFalse(affordances.showCreateAction)
        assertFalse(affordances.showEdit)
        assertFalse(affordances.showPlusUpsell)
        assertTrue(affordances.showLapsedNotice)
    }

    @Test
    fun `a non-member with no schedules gets the upsell instead of the create CTA`() {
        val affordances = RecurringListAffordances.of(RecurringAuthoringGate.Upsell, hasTemplates = false)

        assertTrue(affordances.showPlusUpsell)
        assertFalse(affordances.showCreateAction)
        assertFalse(affordances.showEdit)
        assertFalse(affordances.showLapsedNotice)
    }

    @Test
    fun `a member with schedules gets create and edit and no upsell copy`() {
        val affordances = RecurringListAffordances.of(RecurringAuthoringGate.Allowed, hasTemplates = true)

        assertTrue(affordances.showCreateAction)
        assertTrue(affordances.showEdit)
        assertFalse(affordances.showPlusUpsell)
        assertFalse(affordances.showLapsedNotice)
    }

    /** Nothing replaces the empty state for a member, so its own create CTA renders. */
    @Test
    fun `a member with no schedules keeps the empty-state create CTA`() {
        val affordances = RecurringListAffordances.of(RecurringAuthoringGate.Allowed, hasTemplates = false)

        assertFalse(affordances.showPlusUpsell)
        assertFalse(affordances.showCreateAction)
        assertFalse(affordances.showPausedNotice)
    }

    /**
     * A schedule that still holds a retired entry says so on its card. Only a card that offers Edit tells
     * the customer to edit; a lapsed or paused member's card has no Edit to point at.
     */
    @Test
    fun `a card with Edit says to edit, and one without only says what the schedule holds`() {
        assertEquals(R.string.recurring_card_item_no_longer_offered, retiredEntryLine(showEdit = true))
        assertEquals(R.string.recurring_card_item_no_longer_offered_no_edit, retiredEntryLine(showEdit = false))
    }

    @Test
    fun `a lapsed or paused member's card gets the line without the edit hint`() {
        mapOf(
            RecurringAuthoringGate.Allowed to R.string.recurring_card_item_no_longer_offered,
            RecurringAuthoringGate.Upsell to R.string.recurring_card_item_no_longer_offered_no_edit,
            RecurringAuthoringGate.Paused to R.string.recurring_card_item_no_longer_offered_no_edit,
        ).forEach { (gate, line) ->
            val affordances = RecurringListAffordances.of(gate, hasTemplates = true)
            assertEquals(gate.name, line, retiredEntryLine(affordances.showEdit))
        }
    }

    private fun member(hasMembership: Boolean, status: MembershipStatus? = null) =
        GetMyMembershipResponse(hasMembership = hasMembership, status = status?.code)
}
