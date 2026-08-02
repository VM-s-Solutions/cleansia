package cz.cleansia.customer.features.membership

import cz.cleansia.customer.core.memberships.GetMyMembershipResponse

/**
 * What an active membership actually unlocks, as semantic cases rather than resolved strings, so the
 * card and its test name the same thing.
 *
 * Express upgrade is deliberately absent — here, on the subscribe screen and on the success screen.
 * The plan carries `allowsExpressUpgrade` and the API returns it, but no pricing code reads it —
 * `BookingPolicy.RequiresExpressSurcharge` is lead-time only (2-4h ahead, not "same day"), so a member
 * pays the standard surcharge. Advertising it would promise something the product does not deliver.
 */
sealed interface MembershipPerk {
    data class Discount(val percent: Int) : MembershipPerk

    data class FreeCancellation(val hours: Int) : MembershipPerk

    data object Recurring : MembershipPerk
}

object MembershipPerks {
    fun resolve(membership: GetMyMembershipResponse): List<MembershipPerk> {
        if (!membership.hasMembership) return emptyList()
        return buildList {
            membership.discountPercentage?.toInt()?.takeIf { it > 0 }?.let { add(MembershipPerk.Discount(it)) }
            membership.freeCancellationWindowHours?.takeIf { it > 0 }?.let { add(MembershipPerk.FreeCancellation(it)) }
            // Recurring templates are gated on an active membership and nothing else
            // (`CreateRecurringBooking.Handler`), so membership itself is the backing condition — there
            // is no per-plan flag to read.
            add(MembershipPerk.Recurring)
        }
    }
}
