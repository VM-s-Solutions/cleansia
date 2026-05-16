package cz.cleansia.customer.core.payments

import android.util.Log
import cz.cleansia.core.network.networkCall
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Wraps [PaymentApi] with the same swallow-and-log error handling pattern the
 * other repos use. Used by the booking flow to convert a freshly-created order
 * into the PaymentSheet parameters Stripe needs.
 */
@Singleton
class PaymentRepository @Inject constructor(
    private val api: PaymentApi,
) {
    suspend fun createPaymentIntent(orderId: String): CreatePaymentIntentResponse? {
        val response = networkCall(TAG) { api.createPaymentIntent(CreatePaymentIntentRequest(orderId)) }
            ?: return null
        return if (response.isSuccessful) {
            response.body()
        } else {
            Log.w(TAG, "createPaymentIntent failed for order $orderId: HTTP ${response.code()}")
            null
        }
    }

    private companion object {
        const val TAG = "PaymentRepository"
    }
}
