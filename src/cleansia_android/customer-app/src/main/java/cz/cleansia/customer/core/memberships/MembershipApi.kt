package cz.cleansia.customer.core.memberships

import cz.cleansia.customer.api.client.MembershipApi as GenMembershipApi
import cz.cleansia.customer.api.model.CancelMembershipSubscriptionResponse as GenCancelMembershipSubscriptionResponse
import cz.cleansia.customer.api.model.CreateMembershipSubscriptionCommand as GenCreateMembershipSubscriptionCommand
import cz.cleansia.customer.api.model.CreateMembershipSubscriptionResponse as GenCreateMembershipSubscriptionResponse
import cz.cleansia.customer.api.model.GetMembershipPlansResponse as GenGetMembershipPlansResponse
import cz.cleansia.customer.api.model.GetMyMembershipResponse as GenGetMyMembershipResponse
import cz.cleansia.customer.api.model.MembershipStatus as GenMembershipStatus
import cz.cleansia.customer.api.model.SwapMembershipPlanCommand as GenSwapMembershipPlanCommand
import cz.cleansia.customer.api.model.SwapMembershipPlanResponse as GenSwapMembershipPlanResponse
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenMembershipApi]. Backend route layout
 * mirrors `Cleansia.Web.Customer.Controllers.MembershipController` — all
 * endpoints `[Authorize] + Permission(CanManageMembership)`.
 *
 * Note the generated `MembershipStatus` is a *string* enum (`"Active"` etc.)
 * while the hand-written [GetMyMembershipResponse.status] is `Int?`. We map
 * by enum ordinal-equivalent to match the original `MembershipStatus` enum
 * codes (Active=1, PastDue=2, Cancelled=3, Paused=4).
 */
class MembershipApi(
    private val membershipApi: GenMembershipApi,
) {
    suspend fun subscribe(body: CreateMembershipSubscriptionRequest): Response<CreateMembershipSubscriptionResponse> {
        val raw = membershipApi.membershipSubscribe(
            createMembershipSubscriptionCommand = GenCreateMembershipSubscriptionCommand(
                planCode = body.planCode,
                paymentMethodConfirmed = body.paymentMethodConfirmed,
                // forward the client idempotency token so the
                // backend can derive the Stripe idempotency key from it and collapse
                // retried/double-tapped confirms onto a single subscription.
                //
                // BLOCKED ON nswag-regen (owner-only): the generated
                // GenCreateMembershipSubscriptionCommand does not yet carry the
                // `idempotencyToken` field — the backend Command gained it
                // (CreateMembershipSubscription.Command.IdempotencyToken) but the
                // mobile client hasn't been regenerated. Once the owner regenerates
                // the client, uncomment the line below; the token already flows here
                // from the VM via CreateMembershipSubscriptionRequest.idempotencyToken.
                // idempotencyToken = body.idempotencyToken,
            ),
        )
        return raw.mapBody { it?.toAppDto() }
    }

    suspend fun cancel(): Response<CancelMembershipSubscriptionResponse> {
        val raw = membershipApi.membershipCancel()
        return raw.mapBody { it?.toAppDto() }
    }

    suspend fun getMine(): Response<GetMyMembershipResponse> {
        val raw = membershipApi.membershipGetMine()
        return raw.mapBody { it.toAppDto() }
    }

    /**
     * Refuses the page where the orders list drops the row. The plans are alternatives to each other —
     * the screen shows annual beside monthly with "save 18.5 % vs monthly" — so a silently missing row
     * is not a shorter history but a different purchase, made without the customer ever learning the
     * option existed.
     */
    suspend fun getPlans(): Response<List<MembershipPlanDto>> {
        val raw = membershipApi.membershipGetPlans()
        return raw.mapBody page@{ list -> list.orEmpty().map { it.toAppDto() ?: return@page null } }
    }

    suspend fun swapPlan(body: SwapMembershipPlanRequest): Response<SwapMembershipPlanResponse> {
        val raw = membershipApi.membershipSwapPlan(
            swapMembershipPlanCommand = GenSwapMembershipPlanCommand(newPlanCode = body.newPlanCode),
        )
        return raw.mapBody { it?.toAppDto() }
    }
}

private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

// ─── Generated → app DTO mappers ───

private fun GenCreateMembershipSubscriptionResponse.toAppDto(): CreateMembershipSubscriptionResponse =
    CreateMembershipSubscriptionResponse(
        membershipId = membershipId.orEmpty(),
        setupIntentClientSecret = setupIntentClientSecret.orEmpty(),
        stripeCustomerId = stripeCustomerId.orEmpty(),
        ephemeralKey = ephemeralKey.orEmpty(),
    )

/**
 * The generated DTO only carries `effectiveEndDate` — the hand-written shape
 * also exposes `membershipId`, but no caller reads it post-cancel today, so
 * we fill the empty string here. If a future flow needs it, add it to the
 * backend response first.
 */
private fun GenCancelMembershipSubscriptionResponse.toAppDto(): CancelMembershipSubscriptionResponse =
    CancelMembershipSubscriptionResponse(
        membershipId = "",
        effectiveEndDate = effectiveEndDate?.toString().orEmpty(),
    )

/**
 * `hasMembership` and `cancelRequested` are the two facts every Plus surface gates on, and both
 * defaulted to the answer that costs the member their benefit: `false` hides the discount from someone
 * paying for it, and hides "your plan ends on the 3rd" from someone who asked to cancel. Everything
 * else here is `nullable: true` by design — an absent plan has no price.
 */
private fun GenGetMyMembershipResponse?.toAppDto(): GetMyMembershipResponse? {
    if (this == null) return null
    return GetMyMembershipResponse(
    hasMembership = hasMembership ?: return null,
    // Generated GetMyMembership doesn't expose `membershipId` — repo + UI only
    // gate on `hasMembership`, so null is fine here.
    membershipId = null,
    planCode = planCode,
    planName = planName,
    monthlyPriceCzk = monthlyPriceCzk,
    discountPercentage = discountPercentage,
    freeCancellationWindowHours = freeCancellationWindowHours,
    allowsExpressUpgrade = allowsExpressUpgrade,
    status = status?.toCode() ?: return null,
    currentPeriodEnd = currentPeriodEnd?.toString(),
    cancelRequested = cancelRequested ?: return null,
    billingInterval = billingInterval,
    monthlyEquivalentPriceCzk = monthlyEquivalentPriceCzk,
    expressUpgradesPerMonth = expressUpgradesPerMonth,
    expressUpgradesRemaining = expressUpgradesRemaining,
    trialEndsAtUtc = trialEndsAtUtc,
    )
}

private fun GenMembershipStatus.toCode(): Int = value

/**
 * Every number on a plan card is a term of the subscription the customer is about to buy, so none of
 * them defaults. `billingInterval` defaulting to `1` was the sharpest: it silently reframed an annual
 * plan's price as a monthly one. `allowsExpressUpgrade = false` and `trialPeriodDays = 0` each delete
 * a benefit the card sells, and `savingsPercentVsMonthly = 0.0` deletes the reason to pick the annual
 * plan at all.
 */
private fun GenGetMembershipPlansResponse.toAppDto(): MembershipPlanDto? {
    return MembershipPlanDto(
        code = code ?: return null,
        name = name ?: return null,
        price = price ?: return null,
        monthlyEquivalentPrice = monthlyEquivalentPrice ?: return null,
        billingInterval = billingInterval ?: return null,
        discountPercentage = discountPercentage ?: return null,
        freeCancellationWindowHours = freeCancellationWindowHours ?: return null,
        allowsExpressUpgrade = allowsExpressUpgrade ?: return null,
        trialPeriodDays = trialPeriodDays ?: return null,
        savingsPercentVsMonthly = savingsPercentVsMonthly ?: return null,
    )
}

private fun GenSwapMembershipPlanResponse.toAppDto(): SwapMembershipPlanResponse =
    SwapMembershipPlanResponse(
        membershipId = "",
        newPlanCode = newPlanCode.orEmpty(),
        currentPeriodEnd = currentPeriodEnd?.toString().orEmpty(),
    )
