package cz.cleansia.customer.core.memberships

import kotlinx.serialization.Serializable

/**
 * Mirror of backend `MembershipStatus` enum. Values must match the int
 * positions on the backend — Active=1, PastDue=2, Cancelled=3, Paused=4.
 */
enum class MembershipStatus(val code: Int) {
    Active(1),
    PastDue(2),
    Cancelled(3),
    Paused(4),
    ;

    companion object {
        fun fromCode(code: Int?): MembershipStatus? = when (code) {
            1 -> Active
            2 -> PastDue
            3 -> Cancelled
            4 -> Paused
            else -> null
        }
    }
}

/**
 * Two-phase subscribe request body. First call passes
 * `paymentMethodConfirmed = false` to receive a SetupIntent. After Stripe
 * SDK confirms the SetupIntent, the second call passes `true` to actually
 * create the subscription.
 */
@Serializable
data class CreateMembershipSubscriptionRequest(
    val planCode: String,
    val paymentMethodConfirmed: Boolean = false,
)

/**
 * Mirrors backend `CreateMembershipSubscription.Response`. Discriminate the
 * two phases by [membershipId] being non-empty:
 *  - phase 1 (collect payment method): [setupIntentClientSecret] / [stripeCustomerId] / [ephemeralKey] populated
 *  - phase 2 (subscription created):    [membershipId] populated, secrets are empty
 */
@Serializable
data class CreateMembershipSubscriptionResponse(
    val membershipId: String,
    val setupIntentClientSecret: String,
    val stripeCustomerId: String,
    val ephemeralKey: String,
)

@Serializable
data class CancelMembershipSubscriptionResponse(
    val membershipId: String,
    /** ISO-8601 instant — last day benefits apply before status flips to Cancelled. */
    val effectiveEndDate: String,
)

/**
 * Mirrors backend `GetMyMembership.Response`. When [hasMembership] is false,
 * all other fields are null and the UI shows the upgrade CTA. Otherwise the
 * UI renders the management card with plan + perks + period end + cancel
 * action (gated on [cancelRequested]).
 */
@Serializable
data class GetMyMembershipResponse(
    val hasMembership: Boolean,
    val membershipId: String? = null,
    val planCode: String? = null,
    val planName: String? = null,
    val monthlyPriceCzk: Double? = null,
    val discountPercentage: Double? = null,
    val freeCancellationWindowHours: Int? = null,
    val allowsExpressUpgrade: Boolean? = null,
    /** Backend writes int via JSON serialization; map via [MembershipStatus.fromCode]. */
    val status: Int? = null,
    val currentPeriodEnd: String? = null,
    val cancelRequested: Boolean = false,
    /** 1 = Monthly, 2 = Yearly. Drives "Switch to annual" CTA gating. */
    val billingInterval: Int? = null,
    /** Per-month equivalent: same as [monthlyPriceCzk] for monthly, /12 for yearly. */
    val monthlyEquivalentPriceCzk: Double? = null,
)

/**
 * Mirrors backend `GetMembershipPlans.Response`. Drives the monthly/yearly
 * switcher on the subscribe screen. [savingsPercentVsMonthly] is computed
 * server-side relative to the cheapest monthly plan in the catalog — UI just
 * renders the badge.
 */
@Serializable
data class MembershipPlanDto(
    val code: String,
    val name: String,
    val price: Double,
    val monthlyEquivalentPrice: Double,
    /** 1 = Monthly, 2 = Yearly. */
    val billingInterval: Int,
    val discountPercentage: Double,
    val freeCancellationWindowHours: Int,
    val allowsExpressUpgrade: Boolean,
    val trialPeriodDays: Int,
    val savingsPercentVsMonthly: Double,
)

@Serializable
data class SwapMembershipPlanRequest(
    val newPlanCode: String,
)

@Serializable
data class SwapMembershipPlanResponse(
    val membershipId: String,
    val newPlanCode: String,
    val currentPeriodEnd: String,
)
