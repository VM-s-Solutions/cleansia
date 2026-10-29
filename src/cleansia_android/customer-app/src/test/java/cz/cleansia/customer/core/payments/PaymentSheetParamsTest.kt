package cz.cleansia.customer.core.payments

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import io.mockk.mockk
import io.mockk.verify
import com.stripe.android.paymentsheet.PaymentSheet

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

    @Test
    fun `saved card capture configures customer and presents setup rather than payment intent`() {
        val setup = SavedCardSetup("saved-id", "seti_fixture_secret", "cus_fixture", "ek_fixture")
        val params = setup.toPaymentSheetParams("CZK")
        assertEquals("seti_fixture_secret", params.clientSecret)
        assertEquals("CZK", params.currencyCode)
        assertEquals("cus_fixture", params.toConfiguration().customer?.id)
        assertEquals("ek_fixture", params.toConfiguration().customer?.ephemeralKeySecret)
        val sheet = mockk<PaymentSheet>(relaxed = true)
        sheet.presentSavedCardSetup(setup, "CZK")
        verify(exactly = 1) { sheet.presentWithSetupIntent("seti_fixture_secret", any()) }
        verify(exactly = 0) { sheet.presentWithPaymentIntent(any(), any()) }
        sheet.presentSavedCardSetup(setup, null)
        verify { sheet.presentWithSetupIntent("seti_fixture_secret", match { it.googlePay == null && it.customer?.id == "cus_fixture" }) }
    }
}
