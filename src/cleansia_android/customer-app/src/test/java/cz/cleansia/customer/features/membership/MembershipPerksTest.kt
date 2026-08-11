package cz.cleansia.customer.features.membership

import cz.cleansia.customer.core.memberships.ExpressWaiver
import cz.cleansia.customer.core.memberships.ExpressWaiverStatus
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import kotlinx.datetime.Instant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class MembershipPerksTest {

    private val now = Instant.parse("2026-08-05T10:00:00Z")

    @Test
    fun `inactive membership carries no perks`() {
        assertEquals(emptyList<MembershipPerk>(), MembershipPerks.resolve(inactive, now))
    }

    @Test
    fun `active membership carries discount cancellation and recurring`() {
        assertEquals(
            listOf(
                MembershipPerk.Discount(percent = 5),
                MembershipPerk.FreeCancellation(hours = 4),
                MembershipPerk.Recurring,
            ),
            MembershipPerks.resolve(active, now),
        )
    }

    @Test
    fun `zero discount is omitted`() {
        assertEquals(
            listOf(MembershipPerk.FreeCancellation(hours = 4), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(discountPercentage = 0.0), now),
        )
    }

    @Test
    fun `missing discount is omitted`() {
        assertEquals(
            listOf(MembershipPerk.FreeCancellation(hours = 4), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(discountPercentage = null), now),
        )
    }

    @Test
    fun `zero cancellation window is omitted`() {
        assertEquals(
            listOf(MembershipPerk.Discount(percent = 5), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(freeCancellationWindowHours = 0), now),
        )
    }

    @Test
    fun `missing cancellation window is omitted`() {
        assertEquals(
            listOf(MembershipPerk.Discount(percent = 5), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(freeCancellationWindowHours = null), now),
        )
    }

    @Test
    fun `fractional discount truncates to whole percent`() {
        assertEquals(
            listOf(MembershipPerk.Discount(percent = 7), MembershipPerk.Recurring),
            MembershipPerks.resolve(active.copy(discountPercentage = 7.9, freeCancellationWindowHours = null), now),
        )
    }

    /**
     * `GetMyMembership` reports the resolver's quota, which is already zero for a plan whose express
     * flag is off. Reading the flag as well would give one fact two sources of truth, so a plan
     * carrying it with no quota behind it still resolves no perk.
     */
    @Test
    fun `the express flag alone resolves no perk`() {
        assertTrue(active.allowsExpressUpgrade == true)
        assertEquals(3, MembershipPerks.resolve(active, now).size)
        assertEquals(
            MembershipPerks.resolve(active, now),
            MembershipPerks.resolve(active.copy(allowsExpressUpgrade = false), now),
        )
    }

    @Test
    fun `a plan with express quota left advertises the waiver and its count`() {
        assertEquals(
            listOf(
                MembershipPerk.Discount(percent = 5),
                MembershipPerk.FreeCancellation(hours = 4),
                MembershipPerk.Recurring,
                MembershipPerk.Express(ExpressWaiver(ExpressWaiverStatus.Available, remaining = 2)),
            ),
            MembershipPerks.resolve(withExpress, now),
        )
    }

    @Test
    fun `an exhausted member still sees the perk, reported as used up`() {
        assertEquals(
            MembershipPerk.Express(ExpressWaiver(ExpressWaiverStatus.Exhausted, remaining = 0)),
            MembershipPerks.resolve(withExpress.copy(expressUpgradesRemaining = 0), now).last(),
        )
    }

    @Test
    fun `a trialing member sees the perk, reported as not started`() {
        assertEquals(
            MembershipPerk.Express(ExpressWaiver(ExpressWaiverStatus.Trial, remaining = 0)),
            MembershipPerks.resolve(
                withExpress.copy(
                    trialEndsAtUtc = Instant.parse("2026-08-06T10:00:00Z"),
                    expressUpgradesRemaining = 0,
                ),
                now,
            ).last(),
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
            MembershipPerks.resolve(active.copy(cancelRequested = true), now),
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

    private val withExpress = active.copy(
        expressUpgradesPerMonth = 2,
        expressUpgradesRemaining = 2,
    )
}
