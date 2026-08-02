package cz.cleansia.customer.features.membership

import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class MembershipPerksTest {

    @Test
    fun `inactive membership carries no perks`() {
        assertEquals(emptyList<MembershipPerk>(), MembershipPerks.resolve(inactive))
    }

    @Test
    fun `active membership carries discount cancellation and recurring`() {
        assertEquals(
            listOf(
                MembershipPerk.Discount(percent = 5),
                MembershipPerk.FreeCancellation(hours = 4),
                MembershipPerk.Recurring,
            ),
            MembershipPerks.resolve(active),
        )
    }

    @Test
    fun `zero discount is omitted`() {
        assertEquals(
            listOf(MembershipPerk.FreeCancellation(hours = 4), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(discountPercentage = 0.0)),
        )
    }

    @Test
    fun `missing discount is omitted`() {
        assertEquals(
            listOf(MembershipPerk.FreeCancellation(hours = 4), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(discountPercentage = null)),
        )
    }

    @Test
    fun `zero cancellation window is omitted`() {
        assertEquals(
            listOf(MembershipPerk.Discount(percent = 5), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(freeCancellationWindowHours = 0)),
        )
    }

    @Test
    fun `missing cancellation window is omitted`() {
        assertEquals(
            listOf(MembershipPerk.Discount(percent = 5), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(freeCancellationWindowHours = null)),
        )
    }

    @Test
    fun `fractional discount truncates to whole percent`() {
        assertEquals(
            listOf(MembershipPerk.Discount(percent = 7), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(discountPercentage = 7.9, freeCancellationWindowHours = null)),
        )
    }

    /**
     * `allowsExpressUpgrade` is set on the plan and returned by the API, but its only backend readers
     * are the mapper and two queries — no pricing code consults it, and
     * `BookingPolicy.RequiresExpressSurcharge` is lead-time only. A member pays the same surcharge as
     * everyone else, so a plan that carries the flag must still resolve to no extra perk.
     */
    @Test
    fun `a plan carrying the express flag resolves no extra perk`() {
        assertTrue(active.allowsExpressUpgrade == true)
        assertEquals(3, MembershipPerks.resolve(active).size)
        assertEquals(
            MembershipPerks.resolve(active),
            MembershipPerks.resolve(active.copy(allowsExpressUpgrade = false)),
        )
    }

    @Test
    fun `recurring survives a cancellation request`() {
        assertEquals(
            listOf(
                MembershipPerk.Discount(percent = 5),
                MembershipPerk.FreeCancellation(hours = 4),
                MembershipPerk.Recurring,
            ),
            MembershipPerks.resolve(active.copy(cancelRequested = true)),
        )
    }

    private val inactive = GetMyMembershipResponse(hasMembership = false)

    private val active = GetMyMembershipResponse(
        hasMembership = true,
        planCode = "plus_monthly",
        planName = "Cleansia Plus",
        discountPercentage = 5.0,
        freeCancellationWindowHours = 4,
        allowsExpressUpgrade = true,
        billingInterval = 1,
    )
}
