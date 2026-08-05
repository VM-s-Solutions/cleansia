package cz.cleansia.customer.features.membership

import cz.cleansia.customer.core.memberships.ExpressWaiver
import cz.cleansia.customer.core.memberships.ExpressWaiverStatus
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.resolveExpressWaiver
import kotlinx.datetime.Clock
import kotlinx.datetime.Instant

/**
 * What an active membership actually unlocks, as semantic cases rather than resolved strings, so the
 * card and its test name the same thing.
 */
sealed interface MembershipPerk {
    data class Discount(val percent: Int) : MembershipPerk

    data class FreeCancellation(val hours: Int) : MembershipPerk

    data object Recurring : MembershipPerk

    /**
     * The waived express surcharge. Carries the whole verdict rather than a resolved sentence,
     * because "waived, N left", "used up this month" and "starts when the trial ends" are three
     * different claims and only the server can tell them apart.
     */
    data class Express(val waiver: ExpressWaiver) : MembershipPerk
}

object MembershipPerks {
    fun resolve(
        membership: GetMyMembershipResponse,
        now: Instant = Clock.System.now(),
    ): List<MembershipPerk> {
        if (!membership.hasMembership) return emptyList()
        return buildList {
            membership.discountPercentage?.toInt()?.takeIf { it > 0 }?.let { add(MembershipPerk.Discount(it)) }
            membership.freeCancellationWindowHours?.takeIf { it > 0 }?.let { add(MembershipPerk.FreeCancellation(it)) }
            // Recurring templates are gated on an active membership and nothing else
            // (`CreateRecurringBooking.Handler`), so membership itself is the backing condition — there
            // is no per-plan flag to read.
            add(MembershipPerk.Recurring)
            resolveExpressWaiver(membership, now)
                .takeIf { it.status != ExpressWaiverStatus.None }
                ?.let { add(MembershipPerk.Express(it)) }
        }
    }
}
