package cz.cleansia.customer.core.payments

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import io.mockk.mockk
import io.mockk.verify
import com.stripe.android.paymentsheet.PaymentSheet

/**
 * Given the Stripe customer, PaymentSheet draws its own save box, and a card saved through it stays on the
 * customer with no SavedCards row and no consent. Both the booking and the recurring confirm open the sheet
 * through this configuration. Stripe keeps the configuration's fields internal; its toString names them.
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

        assertTrue("$configuration", "customer=null" in configuration.toString())
    }

    @Test
    fun `a ticked payment configures the sheet on the customer and its ephemeral key`() {
        val configuration = intent.toPaymentSheetParams(saveCard = true, currencyCode = "CZK").toConfiguration()

        assertTrue("$configuration", customer("cus_1", "ek_1") in configuration.toString())
    }

    @Test
    fun `saved card capture configures customer and presents setup rather than payment intent`() {
        val setup = SavedCardSetup("saved-id", "seti_fixture_secret", "cus_fixture", "ek_fixture")
        val params = setup.toPaymentSheetParams("CZK")
        assertEquals("seti_fixture_secret", params.clientSecret)
        assertEquals("CZK", params.currencyCode)
        assertTrue(customer("cus_fixture", "ek_fixture") in params.toConfiguration().toString())
        val sheet = mockk<PaymentSheet>(relaxed = true)
        sheet.presentSavedCardSetup(setup, "CZK")
        verify(exactly = 1) { sheet.presentWithSetupIntent("seti_fixture_secret", any()) }
        verify(exactly = 0) { sheet.presentWithPaymentIntent(any(), any()) }
        sheet.presentSavedCardSetup(setup, null)
        verify {
            sheet.presentWithSetupIntent(
                "seti_fixture_secret",
                match { "googlePay=null" in it.toString() && customer("cus_fixture", "ek_fixture") in it.toString() },
            )
        }
    }

    @Test
    fun `every sheet the app configures turns Stripe Link off`() {
        val setup = SavedCardSetup("saved-id", "seti_fixture_secret", "cus_fixture", "ek_fixture")
        listOf(
            intent.toPaymentSheetParams(saveCard = false, currencyCode = "CZK").toConfiguration(),
            intent.toPaymentSheetParams(saveCard = true, currencyCode = "CZK").toConfiguration(),
            setup.toPaymentSheetParams("CZK").toConfiguration(),
            setup.toPaymentSheetParams(null).toConfiguration(includeGooglePay = false),
        ).forEach { configuration ->
            assertTrue("$configuration", "link=LinkConfiguration(display=Never" in configuration.toString())
        }
    }

    @Test
    fun `every sheet the app presents is configured by toConfiguration`() {
        val main = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
            .map { File(it, "src/main/java/cz/cleansia/customer") }.first { it.isDirectory }
        val code = main.walkTopDown().filter { it.isFile && it.extension == "kt" }
            .associate { it.name to it.readText().replace(COMMENTS, "") }
        assertEquals(listOf("PaymentSheetParams.kt"), code.filterValues { "PaymentSheet.Configuration" in it }.keys.toList())
        val presents = code.flatMap { (name, text) -> PRESENT.findAll(text).map { name to text.argumentsAt(it.range.last) }.toList() }
        assertEquals(
            listOf("BookingBottomSheet.kt", "OrderDetailScreen.kt", "PaymentSheetParams.kt", "SubscribePlusScreen.kt"),
            presents.map { it.first }.sorted(),
        )
        presents.forEach { (name, arguments) -> assertTrue("$name: $arguments", ".toConfiguration(" in arguments) }
    }

    private fun String.argumentsAt(open: Int): String {
        var depth = 0
        for (i in open until length) {
            when (this[i]) {
                '(' -> depth++
                ')' -> if (--depth == 0) return substring(open, i + 1)
            }
        }
        error("unbalanced call at $open")
    }

    private fun customer(id: String, ephemeralKey: String) = "CustomerConfiguration(id=$id, ephemeralKeySecret=$ephemeralKey"

    private companion object {
        val COMMENTS = Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*""")
        val PRESENT = Regex("""\bpresentWith(?:Payment|Setup)Intent\(""")
    }
}
