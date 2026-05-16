package cz.cleansia.customer.core.payments

import kotlinx.serialization.Serializable

@Serializable
data class CreatePaymentIntentRequest(val orderId: String)

/**
 * Mirrors backend `CreatePaymentIntent.Response`. The four pieces below are
 * everything Stripe's PaymentSheet needs to render the bottom sheet, show
 * saved cards, and confirm payment with proper SCA / 3DS handling.
 */
@Serializable
data class CreatePaymentIntentResponse(
    val clientSecret: String,
    val paymentIntentId: String,
    val stripeCustomerId: String,
    val ephemeralKey: String,
)
