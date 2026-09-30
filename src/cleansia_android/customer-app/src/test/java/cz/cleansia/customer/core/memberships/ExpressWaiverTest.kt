package cz.cleansia.customer.core.memberships

import kotlin.time.Duration.Companion.days
import kotlinx.datetime.Clock
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The verdict every express surface renders, from the server's count alone. A trialing member is
 * entitled like a paying one, so the trial end must not change the verdict.
 */
class ExpressWaiverTest {

    private val tomorrow = Clock.System.now() + 1.days
    private val yesterday = Clock.System.now() - 1.days

    @Test
    fun `a missing membership carries no waiver`() {
        assertEquals(ExpressWaiver.None, resolveExpressWaiver(null))
    }

    @Test
    fun `an inactive membership carries no waiver`() {
        assertEquals(
            ExpressWaiver.None,
            resolveExpressWaiver(GetMyMembershipResponse(hasMembership = false)),
        )
    }

    @Test
    fun `a plan with no express quota carries no waiver`() {
        assertEquals(
            ExpressWaiver.None,
            resolveExpressWaiver(active.copy(expressUpgradesPerMonth = null)),
        )
        assertEquals(
            ExpressWaiver.None,
            resolveExpressWaiver(active.copy(expressUpgradesPerMonth = 0)),
        )
    }

    @Test
    fun `a trialing member with quota left has a waiver available`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Available, remaining = 2),
            resolveExpressWaiver(active.copy(trialEndsAtUtc = tomorrow, expressUpgradesRemaining = 2)),
        )
    }

    @Test
    fun `a trialing member with none left is exhausted`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Exhausted, remaining = 0),
            resolveExpressWaiver(active.copy(trialEndsAtUtc = tomorrow, expressUpgradesRemaining = 0)),
        )
    }

    @Test
    fun `a converted trial resolves on the count`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Available, remaining = 2),
            resolveExpressWaiver(active.copy(trialEndsAtUtc = yesterday, expressUpgradesRemaining = 2)),
        )
    }

    @Test
    fun `a paid member with quota left has a waiver available`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Available, remaining = 1),
            resolveExpressWaiver(active.copy(expressUpgradesRemaining = 1)),
        )
    }

    @Test
    fun `a paid member with none left is exhausted`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Exhausted, remaining = 0),
            resolveExpressWaiver(active.copy(expressUpgradesRemaining = 0)),
        )
    }

    @Test
    fun `a missing count is exhausted rather than available`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Exhausted, remaining = 0),
            resolveExpressWaiver(active.copy(expressUpgradesRemaining = null)),
        )
    }

    /**
     * The count is the server's answer to "before the booking under composition". A client that
     * adjusts it disagrees with the server the first time an order is cancelled.
     */
    @Test
    fun `the reported count is the server number untouched`() {
        assertEquals(
            3,
            resolveExpressWaiver(
                active.copy(expressUpgradesPerMonth = 2, expressUpgradesRemaining = 3),
            ).remaining,
        )
    }

    private val active = GetMyMembershipResponse(
        hasMembership = true,
        planCode = "plus_monthly",
        planName = "Cleansia Plus",
        discountPercentage = 5.0,
        freeCancellationWindowHours = 4,
        allowsExpressUpgrade = true,
        billingInterval = 1,
        expressUpgradesPerMonth = 2,
        expressUpgradesRemaining = 2,
    )
}
