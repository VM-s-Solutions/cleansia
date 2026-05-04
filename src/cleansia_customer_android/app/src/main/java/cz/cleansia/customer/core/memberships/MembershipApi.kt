package cz.cleansia.customer.core.memberships

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST

/**
 * Mirrors `Cleansia.Web.Customer.Controllers.MembershipController`. All
 * three endpoints are `[Authorize]` + `Permission(CanManageMembership)` —
 * the AuthRetrofit interceptor handles the bearer token, backend gates by
 * customer role.
 */
interface MembershipApi {
    /**
     * Two-phase: first call (paymentMethodConfirmed=false) returns the
     * SetupIntent for PaymentSheet to attach a payment method. Second call
     * (paymentMethodConfirmed=true) creates the Stripe subscription + local
     * UserMembership row.
     */
    @POST("api/Membership/Subscribe")
    suspend fun subscribe(
        @Body body: CreateMembershipSubscriptionRequest,
    ): Response<CreateMembershipSubscriptionResponse>

    @POST("api/Membership/Cancel")
    suspend fun cancel(): Response<CancelMembershipSubscriptionResponse>

    @GET("api/Membership/GetMine")
    suspend fun getMine(): Response<GetMyMembershipResponse>

    /** Plan catalog — drives the monthly/yearly switcher. Anonymous endpoint. */
    @GET("api/Membership/GetPlans")
    suspend fun getPlans(): Response<List<MembershipPlanDto>>

    /**
     * Swap to a different plan (typically monthly → yearly upgrade). Stripe
     * prorates the cost; user's default payment method is charged/credited.
     */
    @POST("api/Membership/SwapPlan")
    suspend fun swapPlan(@Body body: SwapMembershipPlanRequest): Response<SwapMembershipPlanResponse>
}
