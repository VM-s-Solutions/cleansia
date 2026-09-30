package cz.cleansia.customer.core.memberships

import kotlinx.datetime.Clock
import kotlinx.datetime.Instant
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
 * Two-phase subscribe body: the first call requests a SetupIntent, the second creates the subscription
 * once the SDK has confirmed it.
 *
 * **The idempotency token is generated ONCE per logical attempt and resent unchanged on every phase** —
 * a fresh token per call would create a second subscription.
 * -> /flows/loyalty-and-memberships
 */
@Serializable
data class CreateMembershipSubscriptionRequest(
    val planCode: String,
    val paymentMethodConfirmed: Boolean = false,
    val idempotencyToken: String? = null,
    /** The chosen market's country: the subscription is created in its currency and keeps it for life. */
    val countryId: String? = null,
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
    /** ISO-8601 instant — last day benefits apply before status flips to Cancelled. */
    val effectiveEndDate: String,
)

/**
 * Mirrors backend `GetMyMembership.Response`. When [hasMembership] is false,
 * all other fields are null and the UI shows the upgrade CTA. Otherwise the
 * UI renders the management card with plan + perks + period end + cancel
 * action (gated on [cancelRequested]).
 *
 * [price] and [monthlyEquivalentPrice] are stated in [currencyCode] — the subscription's own
 * currency, which may differ from the market the customer browses in (ADR-0059 D2).
 */
@Serializable
data class GetMyMembershipResponse(
    val hasMembership: Boolean,
    val planCode: String? = null,
    val planName: String? = null,
    val price: Double? = null,
    val discountPercentage: Double? = null,
    val freeCancellationWindowHours: Int? = null,
    val allowsExpressUpgrade: Boolean? = null,
    /** Backend writes int via JSON serialization; map via [MembershipStatus.fromCode]. */
    val status: Int? = null,
    val currentPeriodEnd: String? = null,
    val cancelRequested: Boolean = false,
    /** 1 = Monthly, 2 = Yearly. Drives "Switch to annual" CTA gating. */
    val billingInterval: Int? = null,
    /** Per-month equivalent: same as [price] for monthly, /12 for yearly. */
    val monthlyEquivalentPrice: Double? = null,
    /** The resolver's quota, already zero for a plan whose express flag is off. */
    val expressUpgradesPerMonth: Int? = null,
    /**
     * Live waivers left this calendar month, before any booking under composition. Null = no
     * membership; 0 = exhausted, or a past-due or paused enrolment.
     */
    val expressUpgradesRemaining: Int? = null,
    /** End of the Stripe free trial. In the future means the first payment falls on it. */
    val trialEndsAtUtc: kotlinx.datetime.Instant? = null,
    /** False once this customer has had their one free trial. Absent reads as false: no trial is promised unconfirmed. */
    val trialEligible: Boolean = false,
    /** Null only for a non-member, or when the plan's price row in this currency was deleted. */
    val currencyCode: String? = null,
)

/**
 * When the running free trial ends, or null. A trialing member has every Plus benefit; the trial only
 * decides when the first payment falls. -> /product/business-rules
 */
fun GetMyMembershipResponse.trialEndsAt(now: Instant = Clock.System.now()): Instant? =
    trialEndsAtUtc?.takeIf { hasMembership && it > now }

/**
 * The free-trial days this customer would get on [plan]: the plan's own while they have never had a
 * trial, else 0 — one trial per account, and the server bills anyone who has had it from day one.
 */
fun GetMyMembershipResponse?.trialDaysOn(plan: MembershipPlanDto?): Int =
    if (this?.trialEligible == true && plan != null) plan.trialPeriodDays else 0

/** The trial a surface without a plan picker offers: the monthly plan's, the one every Plus surface leads with. */
fun GetMyMembershipResponse?.headlineTrialDays(plans: List<MembershipPlanDto>): Int =
    trialDaysOn(plans.firstOrNull { it.billingInterval == 1 } ?: plans.firstOrNull())

/**
 * A live enrolment whose renewal payment failed, or that Stripe paused. [GetMyMembershipResponse.hasMembership]
 * still counts it — the server refuses a second subscription while it lives — but no Plus benefit runs, and
 * its cancel takes effect at once. -> /product/business-rules
 */
val GetMyMembershipResponse.benefitsPaused: Boolean
    get() = hasMembership &&
        MembershipStatus.fromCode(status).let { it == MembershipStatus.PastDue || it == MembershipStatus.Paused }

/**
 * Mirrors backend `GetMembershipPlans.Response`. Drives the monthly/yearly
 * switcher on the subscribe screen. [savingsPercentVsMonthly] is computed
 * server-side relative to the cheapest monthly plan in the catalog — UI just
 * renders the badge. Every figure is in [currencyCode], the market's currency;
 * an empty list means Plus is not on sale in that market (ADR-0059 D3).
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
    /** How many free express upgrades the plan grants each calendar month. */
    val expressUpgradesPerMonth: Int? = null,
    val allowsExpressUpgrade: Boolean,
    val trialPeriodDays: Int,
    val savingsPercentVsMonthly: Double,
    val currencyCode: String,
)

@Serializable
data class SwapMembershipPlanRequest(
    val newPlanCode: String,
)

@Serializable
data class SwapMembershipPlanResponse(
    val newPlanCode: String,
    val currentPeriodEnd: String,
)
