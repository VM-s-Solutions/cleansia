package cz.cleansia.customer.core.memberships

import kotlinx.datetime.Clock
import kotlinx.datetime.Instant

/**
 * What a customer surface may say about the express surcharge.
 *
 *  - [None] — no active membership, or a plan carrying no express quota. Say nothing; the standard
 *    surcharge label already tells the customer they are being charged.
 *  - [Trial] — the plan carries a quota but the trial has not converted to a paid month, so nothing is
 *    waived yet.
 *  - [Available] — at least one waiver left in the current calendar month.
 *  - [Exhausted] — the quota is used up until the calendar month rolls over.
 */
enum class ExpressWaiverStatus { None, Trial, Available, Exhausted }

/**
 * [remaining] is the server's count before the booking under composition, rendered verbatim. It is 0
 * for a trialing member and for an exhausted one alike, which is why [status] carries the difference.
 */
data class ExpressWaiver(
    val status: ExpressWaiverStatus,
    val remaining: Int,
) {
    companion object {
        val None = ExpressWaiver(ExpressWaiverStatus.None, remaining = 0)
    }
}

/**
 * Resolved here rather than in a feature package because the booking wizard and the membership
 * screens must not answer it two different ways.
 *
 * `expressUpgradesRemaining` is 0 for a trialing member for the same reason an exhausted member's is,
 * and `trialEndsAtUtc` is the only field that separates them — telling a trial member they used theirs
 * up would be a new false claim. The quota, not `allowsExpressUpgrade`, is the gate: the server
 * already reports zero for a plan whose flag is off, so reading both would give one fact two sources.
 */
fun resolveExpressWaiver(
    membership: GetMyMembershipResponse?,
    now: Instant = Clock.System.now(),
): ExpressWaiver {
    if (membership?.hasMembership != true) return ExpressWaiver.None
    if ((membership.expressUpgradesPerMonth ?: 0) <= 0) return ExpressWaiver.None

    val trialEndsAtUtc = membership.trialEndsAtUtc
    if (trialEndsAtUtc != null && trialEndsAtUtc > now) {
        return ExpressWaiver(ExpressWaiverStatus.Trial, remaining = 0)
    }

    val remaining = membership.expressUpgradesRemaining ?: 0
    return if (remaining > 0) {
        ExpressWaiver(ExpressWaiverStatus.Available, remaining = remaining)
    } else {
        ExpressWaiver(ExpressWaiverStatus.Exhausted, remaining = 0)
    }
}
