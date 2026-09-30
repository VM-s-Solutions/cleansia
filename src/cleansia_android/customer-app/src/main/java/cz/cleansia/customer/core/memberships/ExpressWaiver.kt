package cz.cleansia.customer.core.memberships

/**
 * What a customer surface may say about the express surcharge.
 *
 *  - [None] — no active membership, or a plan carrying no express quota. Say nothing; the standard
 *    surcharge label already tells the customer they are being charged.
 *  - [Available] — at least one waiver left in the current calendar month.
 *  - [Exhausted] — the quota is used up until the calendar month rolls over.
 */
enum class ExpressWaiverStatus { None, Available, Exhausted }

/** [remaining] is the server's count before the booking under composition, rendered verbatim. */
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
 * The quota, not `allowsExpressUpgrade`, is the gate: the server already reports zero for a plan whose
 * flag is off, so reading both would give one fact two sources. A trialing member is entitled like a
 * paying one, so the server's count is the whole verdict.
 */
fun resolveExpressWaiver(membership: GetMyMembershipResponse?): ExpressWaiver {
    if (membership?.hasMembership != true) return ExpressWaiver.None
    if ((membership.expressUpgradesPerMonth ?: 0) <= 0) return ExpressWaiver.None

    val remaining = membership.expressUpgradesRemaining ?: 0
    return if (remaining > 0) {
        ExpressWaiver(ExpressWaiverStatus.Available, remaining = remaining)
    } else {
        ExpressWaiver(ExpressWaiverStatus.Exhausted, remaining = 0)
    }
}
