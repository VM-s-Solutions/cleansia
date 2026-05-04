package cz.cleansia.customer.core.promo

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST

/**
 * Retrofit binding for the customer PromoCode endpoint.
 *
 * Routes mirror `Cleansia.Web.Customer.Controllers.PromoCodeController`:
 *  - `POST /api/PromoCode/Validate` — preview-validate a code against a subtotal.
 *
 * Requires auth (`Permission(CanRedeemPromoCode)`) — the AuthInterceptor
 * attaches the Bearer token automatically (route is not in the anon allowlist).
 *
 * Note: actual redemption happens server-side inside `CreateOrder.Handler` —
 * the client never calls a "redeem" endpoint. This validate call is purely a
 * UX pre-check so we can show a green/red state before the user submits.
 */
interface PromoCodeApi {
    @POST("api/PromoCode/Validate")
    suspend fun validate(@Body body: ValidatePromoCodeRequest): Response<ValidatePromoCodeResponse>
}
