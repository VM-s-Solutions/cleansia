package cz.cleansia.customer.features.booking

import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The cancellation policy card's arithmetic.
 *
 * It shipped with the Plus comparison INVERTED — `> standardFreeHours` where the perk is a smaller
 * number — and nothing could see it, because the rule lived inside a `@Composable` and the only way
 * to exercise it was to render the screen. iOS held the identical mistake, and its tests asserted
 * the wrong direction, so both apps read green while telling a paying member they had to cancel 24 h
 * ahead to cancel free.
 *
 * The direction, from `BookingPolicy.ClassifyCancellation`: free when `hoursBeforeStart >=
 * freeWindow`. A plan window of 4 therefore means free right up to 4 h before, where a non-member
 * pays 25 % from 24 h — so the BENEFIT is the deadline moving closer to the cleaning, and the number
 * is SMALLER. The seeded plans carry 4.
 */
class CancellationPolicyDisplayTest {

    private fun membership(hasMembership: Boolean, freeHours: Int?) = GetMyMembershipResponse(
        hasMembership = hasMembership,
        freeCancellationWindowHours = freeHours,
    )

    @Test
    fun `no membership falls back to the standard window`() {
        val policy = cancellationPolicyFor(null)

        assertEquals(24, policy.freeHours)
        assertEquals(4, policy.penaltyHours)
        assertNull(policy.plusFreeHours)
        assertTrue(policy.showMidTier)
        assertFalse(policy.hasPlusPerk)
    }

    @Test
    fun `a window on a lapsed membership is ignored`() {
        val policy = cancellationPolicyFor(membership(hasMembership = false, freeHours = 4))

        assertEquals(24, policy.freeHours)
        assertNull(policy.plusFreeHours)
    }

    /** The real shape: the seeded plans carry 4 against a standard 24. */
    @Test
    fun `a window closer to the cleaning is the perk`() {
        val policy = cancellationPolicyFor(membership(hasMembership = true, freeHours = 4))

        assertEquals(4, policy.freeHours)
        assertEquals(4, policy.plusFreeHours)
        assertTrue(policy.hasPlusPerk)
        // Free until 4 h, then the last-minute rate under 4 h. There is no 4-to-4 band left, so the
        // mid tier is correctly absent rather than missing.
        assertFalse(policy.showMidTier)
    }

    @Test
    fun `a window that still leaves room keeps the mid tier`() {
        val policy = cancellationPolicyFor(membership(hasMembership = true, freeHours = 12))

        assertEquals(12, policy.freeHours)
        assertTrue(policy.hasPlusPerk)
        assertTrue(policy.showMidTier)
    }

    /**
     * The one the old comparison got backwards. A window FURTHER out than the standard would make a
     * member cancel EARLIER than a non-member to pay nothing — not a benefit, so it is ignored
     * rather than advertised.
     */
    @Test
    fun `a window further out than standard is not a perk`() {
        val policy = cancellationPolicyFor(membership(hasMembership = true, freeHours = 48))

        assertEquals(24, policy.freeHours)
        assertNull(policy.plusFreeHours)
        assertFalse(policy.hasPlusPerk)
    }

    @Test
    fun `a window equal to standard is not a perk`() {
        val policy = cancellationPolicyFor(membership(hasMembership = true, freeHours = 24))

        assertEquals(24, policy.freeHours)
        assertNull(policy.plusFreeHours)
        assertFalse(policy.hasPlusPerk)
    }

    @Test
    fun `a zero or absent window falls back to the standard one`() {
        assertNull(cancellationPolicyFor(membership(true, 0)).plusFreeHours)
        assertNull(cancellationPolicyFor(membership(true, null)).plusFreeHours)
        assertEquals(24, cancellationPolicyFor(membership(true, 0)).freeHours)
    }
}
