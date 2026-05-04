package cz.cleansia.customer.core.loyalty

import retrofit2.Response
import retrofit2.http.GET
import retrofit2.http.Query

/**
 * Retrofit binding for the customer Loyalty endpoints.
 *
 * Routes mirror [Cleansia.Web.Customer.Controllers.LoyaltyController]:
 *  - `GET /api/Loyalty/GetMy`        → account snapshot (tier, points, perks)
 *  - `GET /api/Loyalty/GetActivity`  → paged transaction history
 *  - `GET /api/Loyalty/GetTiers`     → all tier configs (for the ladder UI)
 *
 * All require auth (`Permission(CanViewMyLoyalty)`) — the AuthInterceptor
 * attaches the Bearer token automatically since none of these match the
 * anon-endpoint allowlist.
 */
interface LoyaltyApi {
    @GET("api/Loyalty/GetMy")
    suspend fun getMy(): Response<LoyaltyAccountDto>

    @GET("api/Loyalty/GetActivity")
    suspend fun getActivity(
        @Query("offset") offset: Int = 0,
        @Query("limit") limit: Int = 20,
    ): Response<LoyaltyActivityResponseDto>

    @GET("api/Loyalty/GetTiers")
    suspend fun getTiers(): Response<LoyaltyTiersResponseDto>
}
