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

    suspend fun getPlans(): Response<List<MembershipPlanDto>> {
        val raw = membershipApi.membershipGetPlans()
        return raw.mapBody { list -> list?.mapNotNull { it.toAppDto() }.orEmpty() }
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

private fun GenGetMyMembershipResponse?.toAppDto(): GetMyMembershipResponse = GetMyMembershipResponse(
    hasMembership = this?.hasMembership ?: false,
    // Generated GetMyMembership doesn't expose `membershipId` — repo + UI only
    // gate on `hasMembership`, so null is fine here.
    membershipId = null,
    planCode = this?.planCode,
    planName = this?.planName,
    monthlyPriceCzk = this?.monthlyPriceCzk,
    discountPercentage = this?.discountPercentage,
    freeCancellationWindowHours = this?.freeCancellationWindowHours,
    allowsExpressUpgrade = this?.allowsExpressUpgrade,
    status = this?.status?.toCode(),
    currentPeriodEnd = this?.currentPeriodEnd?.toString(),
    cancelRequested = this?.cancelRequested ?: false,
    billingInterval = this?.billingInterval,
    monthlyEquivalentPriceCzk = this?.monthlyEquivalentPriceCzk,
)

private fun GenMembershipStatus.toCode(): Int = when (this) {
    GenMembershipStatus.ACTIVE -> MembershipStatus.Active.code
    GenMembershipStatus.PAST_DUE -> MembershipStatus.PastDue.code
    GenMembershipStatus.CANCELLED -> MembershipStatus.Cancelled.code
    GenMembershipStatus.PAUSED -> MembershipStatus.Paused.code
}

private fun GenGetMembershipPlansResponse.toAppDto(): MembershipPlanDto? {
    val code = code ?: return null
    val name = name ?: return null
    return MembershipPlanDto(
        code = code,
        name = name,
        price = price ?: 0.0,
        monthlyEquivalentPrice = monthlyEquivalentPrice ?: 0.0,
        billingInterval = billingInterval ?: 1,
        discountPercentage = discountPercentage ?: 0.0,
        freeCancellationWindowHours = freeCancellationWindowHours ?: 0,
        allowsExpressUpgrade = allowsExpressUpgrade ?: false,
        trialPeriodDays = trialPeriodDays ?: 0,
        savingsPercentVsMonthly = savingsPercentVsMonthly ?: 0.0,
    )
}

private fun GenSwapMembershipPlanResponse.toAppDto(): SwapMembershipPlanResponse =
    SwapMembershipPlanResponse(
        membershipId = "",
        newPlanCode = newPlanCode.orEmpty(),
        currentPeriodEnd = currentPeriodEnd?.toString().orEmpty(),
    )
