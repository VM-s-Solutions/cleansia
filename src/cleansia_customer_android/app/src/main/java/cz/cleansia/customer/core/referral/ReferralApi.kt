package cz.cleansia.customer.core.referral

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Query

/**
 * Retrofit binding for the customer Referral endpoints (Loyalty Phase C).
 *
 * Routes mirror `Cleansia.Web.Customer.Controllers.ReferralController`:
 *  - `GET  /api/Referral/GetMy`         — current user's code + counters.
 *  - `GET  /api/Referral/GetMyReferrals` — paged who-I-invited list.
 *  - `POST /api/Referral/Validate`      — pre-check a code (public).
 *
 * GetMy/GetMyReferrals are gated by `Permission(CanViewMyReferral)` so they
 * need a Bearer token. Validate is `[AllowAnonymous]` — the [AuthInterceptor]
 * on `@AuthRetrofit` already skips the Authorization header when [TokenStore]
 * holds nothing (signup form), so a single Retrofit instance serves all three.
 */
interface ReferralApi {
    @GET("api/Referral/GetMy")
    suspend fun getMy(): Response<ReferralAccountDto>

    @GET("api/Referral/GetMyReferrals")
    suspend fun getMyReferrals(
        @Query("offset") offset: Int = 0,
        @Query("limit") limit: Int = 20,
    ): Response<ReferralListResponseDto>

    @POST("api/Referral/Validate")
    suspend fun validate(@Body body: ValidateReferralRequest): Response<ValidateReferralResponse>
}
