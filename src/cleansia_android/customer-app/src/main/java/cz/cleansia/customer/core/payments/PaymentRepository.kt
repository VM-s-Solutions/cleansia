package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Wraps [PaymentApi] for the booking flow, converting a freshly-created order
 * into the PaymentSheet parameters Stripe needs. Returns [ApiResult] so the
 * ViewModel surfaces any failure and the repo stays UI-free.
 */
@Singleton
class PaymentRepository @Inject constructor(
    private val api: PaymentApi,
    private val json: Json,
) {
    suspend fun createPaymentIntent(orderId: String): ApiResult<CreatePaymentIntentResponse> =
        safeApiCall(json) { api.createPaymentIntent(CreatePaymentIntentRequest(orderId)) }
}
