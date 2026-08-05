package cz.cleansia.customer.core.memberships

import kotlinx.datetime.Instant
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The server reports `expressUpgradesRemaining = 0` for a trialing member AND for an exhausted one,
 * and `trialEndsAtUtc` is the only field that separates them. Telling a trial member they used theirs
 * up is a false claim, so both zeros are pinned here rather than left to the screens.
 */
class ExpressWaiverTest {

    private val now = Instant.parse("2026-08-05T10:00:00Z")
    private val tomorrow = Instant.parse("2026-08-06T10:00:00Z")
    private val yesterday = Instant.parse("2026-08-04T10:00:00Z")

    @Test
    fun `a missing membership carries no waiver`() {
        assertEquals(ExpressWaiver.None, resolveExpressWaiver(null, now))
    }

    @Test
    fun `an inactive membership carries no waiver`() {
        assertEquals(
            ExpressWaiver.None,
            resolveExpressWaiver(GetMyMembershipResponse(hasMembership = false), now),
        )
    }

    @Test
    fun `a plan with no express quota carries no waiver`() {
        assertEquals(
            ExpressWaiver.None,
            resolveExpressWaiver(active.copy(expressUpgradesPerMonth = null), now),
        )
        assertEquals(
            ExpressWaiver.None,
            resolveExpressWaiver(active.copy(expressUpgradesPerMonth = 0), now),
        )
    }

    @Test
    fun `a trialing member earns no waiver yet`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Trial, remaining = 0),
            resolveExpressWaiver(active.copy(trialEndsAtUtc = tomorrow, expressUpgradesRemaining = 0), now),
        )
    }

    /** The trial verdict does not depend on the count, so a non-zero one cannot leak a waiver. */
    @Test
    fun `a trialing member earns no waiver even when the count is not zero`() {
        assertEquals(
            ExpressWaiverStatus.Trial,
            resolveExpressWaiver(active.copy(trialEndsAtUtc = tomorrow, expressUpgradesRemaining = 2), now).status,
        )
    }

    @Test
    fun `a converted trial resolves on the count again`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Available, remaining = 2),
            resolveExpressWaiver(active.copy(trialEndsAtUtc = yesterday, expressUpgradesRemaining = 2), now),
        )
    }

    @Test
    fun `a paid member with quota left has a waiver available`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Available, remaining = 1),
            resolveExpressWaiver(active.copy(expressUpgradesRemaining = 1), now),
        )
    }

    @Test
    fun `a paid member with none left is exhausted`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Exhausted, remaining = 0),
            resolveExpressWaiver(active.copy(expressUpgradesRemaining = 0), now),
        )
    }

    @Test
    fun `a missing count is exhausted rather than available`() {
        assertEquals(
            ExpressWaiver(ExpressWaiverStatus.Exhausted, remaining = 0),
            resolveExpressWaiver(active.copy(expressUpgradesRemaining = null), now),
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
                now,
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
