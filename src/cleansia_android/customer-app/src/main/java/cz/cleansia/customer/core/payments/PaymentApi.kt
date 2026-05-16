package cz.cleansia.customer.core.payments

import cz.cleansia.customer.api.client.PaymentApi as GenPaymentApi
import cz.cleansia.customer.api.model.CreatePaymentIntentCommand as GenCreatePaymentIntentCommand
import cz.cleansia.customer.api.model.CreatePaymentIntentResponse as GenCreatePaymentIntentResponse
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenPaymentApi]. Customer-side Stripe
 * PaymentIntent provisioning — the four returned strings are everything the
 * Stripe SDK's PaymentSheet needs to render the bottom sheet.
 */
class PaymentApi(
    private val paymentApi: GenPaymentApi,
) {
    suspend fun createPaymentIntent(body: CreatePaymentIntentRequest): Response<CreatePaymentIntentResponse> {
        val raw = paymentApi.paymentCreatePaymentIntent(
            createPaymentIntentCommand = GenCreatePaymentIntentCommand(orderId = body.orderId),
        )
        return raw.mapBody { it?.toAppDto() }
    }
}

private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

/**
 * Stripe PaymentSheet treats any of the four fields below as a hard failure
 * if missing, so a malformed payload is better dropped here than surfaced as
 * partial nulls.
 */
private fun GenCreatePaymentIntentResponse.toAppDto(): CreatePaymentIntentResponse? {
    val clientSecret = clientSecret ?: return null
    val paymentIntentId = paymentIntentId ?: return null
    val stripeCustomerId = stripeCustomerId ?: return null
    val ephemeralKey = ephemeralKey ?: return null
    return CreatePaymentIntentResponse(
        clientSecret = clientSecret,
        paymentIntentId = paymentIntentId,
        stripeCustomerId = stripeCustomerId,
        ephemeralKey = ephemeralKey,
    )
}
