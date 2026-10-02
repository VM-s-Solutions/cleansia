package cz.cleansia.customer.core.payments

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * Given the Stripe customer, PaymentSheet draws its own save box, and a card saved through it stays on the
 * customer with no SavedCards row and no consent. Both the booking and the recurring confirm open the sheet
 * through this configuration.
 */
class PaymentSheetParamsTest {

    private val intent = CreatePaymentIntentResponse(
        clientSecret = "pi_secret",
        paymentIntentId = "pi_1",
        stripeCustomerId = "cus_1",
        ephemeralKey = "ek_1",
    )

    @Test
    fun `an unticked payment configures the sheet without the customer`() {
        val configuration = intent.toPaymentSheetParams(saveCard = false, currencyCode = "CZK").toConfiguration()

        assertNull(configuration.customer)
    }

    @Test
    fun `a ticked payment configures the sheet on the customer and its ephemeral key`() {
        val customer = intent.toPaymentSheetParams(saveCard = true, currencyCode = "CZK").toConfiguration().customer

        assertEquals("cus_1", customer?.id)
        assertEquals("ek_1", customer?.ephemeralKeySecret)
    }
}
