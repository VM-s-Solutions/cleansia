package cz.cleansia.customer.features.recurring

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
        assertEquals(RecurringAuthoringGate.Upsell, RecurringAuthoringGate.resolve(false))
    }

    @Test
    fun `a member may author`() {
        assertEquals(RecurringAuthoringGate.Allowed, RecurringAuthoringGate.resolve(true))
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
    }
}
