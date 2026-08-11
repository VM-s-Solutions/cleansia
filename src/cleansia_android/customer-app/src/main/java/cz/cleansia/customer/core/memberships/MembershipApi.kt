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
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenMembershipApi]. Backend route layout
 * mirrors `Cleansia.Web.Customer.Controllers.MembershipController` — all
 * endpoints `[Authorize] + Permission(CanManageMembership)`.
 *
 * The generated `MembershipStatus` decodes through `IntEnumSerializersModule`, so its `value` is
 * already the backend's code (Active=1, PastDue=2, Cancelled=3, Paused=4), which is what the
 * hand-written [GetMyMembershipResponse.status] carries.
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
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun cancel(): Response<CancelMembershipSubscriptionResponse> {
        val raw = membershipApi.membershipCancel()
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun getMine(): Response<GetMyMembershipResponse> {
        val raw = membershipApi.membershipGetMine()
        return raw.mapWire { it.toAppDto() }
    }

    /**
     * Refuses the page where the orders list drops the row. The plans are alternatives to each other —
     * the screen shows annual beside monthly with "save 18.5 % vs monthly" — so a silently missing row
     * is not a shorter history but a different purchase, made without the customer ever learning the
     * option existed.
     *
     * The body is refused for the same reason rather than defaulted to empty: an absent plan list is
     * not "Plus is unavailable today", and emptiness here deletes the subscribe CTA outright.
     */
    suspend fun getPlans(): Response<List<MembershipPlanDto>> {
        val raw = membershipApi.membershipGetPlans()
        return raw.mapWire { list -> list.required("GetMembershipPlansResponse").map { it.toAppDto() } }
    }

    suspend fun swapPlan(body: SwapMembershipPlanRequest): Response<SwapMembershipPlanResponse> {
        val raw = membershipApi.membershipSwapPlan(
            swapMembershipPlanCommand = GenSwapMembershipPlanCommand(newPlanCode = body.newPlanCode),
        )
        return raw.mapWire { it.toAppDto() }
    }
}

// ─── Generated → app DTO mappers ───

/**
 * Every field here is a credential the Stripe PaymentSheet is opened with, and all four are
 * non-nullable on `CreateMembershipSubscription.Response`. Blanked, the sheet fails with nothing on
 * screen or in the log to say which credential was missing — a refusal naming the field is the whole
 * difference between a diagnosable failure and an unexplained one. The spec calls all four
 * `nullable: true`, as it does every string on this wire; the C# record is the contract.
 */
private fun GenCreateMembershipSubscriptionResponse?.toAppDto(): CreateMembershipSubscriptionResponse {
    val started = required("CreateMembershipSubscriptionResponse")
    return CreateMembershipSubscriptionResponse(
        membershipId = started.membershipId.required("membershipId"),
        setupIntentClientSecret = started.setupIntentClientSecret.required("setupIntentClientSecret"),
        stripeCustomerId = started.stripeCustomerId.required("stripeCustomerId"),
        ephemeralKey = started.ephemeralKey.required("ephemeralKey"),
    )
}

/**
 * `effectiveEndDate` is `DateTime` (non-nullable) and it is the whole content of the confirmation:
 * blank, the sheet says the membership ends on nothing.
 */
private fun GenCancelMembershipSubscriptionResponse?.toAppDto(): CancelMembershipSubscriptionResponse =
    CancelMembershipSubscriptionResponse(
        effectiveEndDate = required("CancelMembershipSubscriptionResponse")
            .effectiveEndDate?.toString().required("effectiveEndDate"),
    )

/**
 * `hasMembership` and `cancelRequested` are the two facts every Plus surface gates on, and both
 * defaulted to the answer that costs the member their benefit: `false` hides the discount from someone
 * paying for it, and hides "your plan ends on the 3rd" from someone who asked to cancel. Both are
 * non-nullable `bool` on `GetMyMembership.Response`, so they refuse.
 *
 * `status` does NOT, and the difference matters more than it looks: it is `MembershipStatus?` in C#
 * and the handler's non-member branch sends it null alongside every other plan field. Refusing on it
 * turned the ordinary "you are not a member yet" answer into an error screen for everyone who has
 * never subscribed — which is the whole audience the upgrade CTA exists for.
 */
private fun GenGetMyMembershipResponse?.toAppDto(): GetMyMembershipResponse {
    val mine = required("GetMyMembershipResponse")
    return GetMyMembershipResponse(
        hasMembership = mine.hasMembership.required("hasMembership"),
        planCode = mine.planCode,
        planName = mine.planName,
        monthlyPriceCzk = mine.monthlyPriceCzk,
        discountPercentage = mine.discountPercentage,
        freeCancellationWindowHours = mine.freeCancellationWindowHours,
        allowsExpressUpgrade = mine.allowsExpressUpgrade,
        status = mine.status?.toCode(),
        currentPeriodEnd = mine.currentPeriodEnd?.toString(),
        cancelRequested = mine.cancelRequested.required("cancelRequested"),
        billingInterval = mine.billingInterval,
        monthlyEquivalentPriceCzk = mine.monthlyEquivalentPriceCzk,
        expressUpgradesPerMonth = mine.expressUpgradesPerMonth,
        expressUpgradesRemaining = mine.expressUpgradesRemaining,
        trialEndsAtUtc = mine.trialEndsAtUtc,
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
private fun GenGetMembershipPlansResponse.toAppDto(): MembershipPlanDto =
    MembershipPlanDto(
        code = code.required("code"),
        name = name.required("name"),
        price = price.required("price"),
        monthlyEquivalentPrice = monthlyEquivalentPrice.required("monthlyEquivalentPrice"),
        billingInterval = billingInterval.required("billingInterval"),
        discountPercentage = discountPercentage.required("discountPercentage"),
        freeCancellationWindowHours = freeCancellationWindowHours.required("freeCancellationWindowHours"),
        allowsExpressUpgrade = allowsExpressUpgrade.required("allowsExpressUpgrade"),
        trialPeriodDays = trialPeriodDays.required("trialPeriodDays"),
        savingsPercentVsMonthly = savingsPercentVsMonthly.required("savingsPercentVsMonthly"),
    )

/**
 * Both are non-nullable on `SwapMembershipPlan.Response` — unlike `GetMyMembership`'s
 * `currentPeriodEnd`, which is `DateTime?` because a non-member has no period, and stays nullable.
 */
private fun GenSwapMembershipPlanResponse?.toAppDto(): SwapMembershipPlanResponse {
    val swapped = required("SwapMembershipPlanResponse")
    return SwapMembershipPlanResponse(
        newPlanCode = swapped.newPlanCode.required("newPlanCode"),
        currentPeriodEnd = swapped.currentPeriodEnd?.toString().required("currentPeriodEnd"),
    )
}
