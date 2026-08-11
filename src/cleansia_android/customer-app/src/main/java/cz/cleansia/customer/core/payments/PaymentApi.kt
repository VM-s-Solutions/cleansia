package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
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
        return raw.mapWire { it.toAppDto() }
    }
}

/**
 * All four are non-nullable on `CreatePaymentIntent.Response`, and each is a credential the
 * PaymentSheet is opened with. Dropping the body instead said "the server sent nothing" for a body it
 * did send, on the screen where a customer is trying to pay — the refusal names the missing
 * credential instead. The spec calls all four `nullable: true`, as it does every string on this wire;
 * the C# record is the contract.
 */
private fun GenCreatePaymentIntentResponse?.toAppDto(): CreatePaymentIntentResponse {
    val intent = required("CreatePaymentIntentResponse")
    return CreatePaymentIntentResponse(
        clientSecret = intent.clientSecret.required("clientSecret"),
        paymentIntentId = intent.paymentIntentId.required("paymentIntentId"),
        stripeCustomerId = intent.stripeCustomerId.required("stripeCustomerId"),
        ephemeralKey = intent.ephemeralKey.required("ephemeralKey"),
    )
}
