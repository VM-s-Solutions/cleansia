package cz.cleansia.customer.core.payments

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST

/**
 * Customer Payment endpoints. Matches `Cleansia.Web.Customer.Controllers.PaymentController`.
 * The CreatePaymentIntent endpoint is `[Authorize]` — the AuthRetrofit Hilt
 * qualifier wires this through the bearer-token interceptor automatically.
 */
interface PaymentApi {
    @POST("api/Payment/CreatePaymentIntent")
    suspend fun createPaymentIntent(
        @Body body: CreatePaymentIntentRequest,
    ): Response<CreatePaymentIntentResponse>
}
