package cz.cleansia.customer.core.payments

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Stripe paymentsheet 21.2.0 draws saved cards in its horizontal layout through a Compose member that
 * Compose 1.8 removed, so on this app's Compose that layout throws NoSuchMethodError. Every sheet the app
 * configures asks for the vertical layout, so neither a Stripe default nor a new configuration can reach it.
 */
class PaymentSheetLayoutTest {

    @Test
    fun `the booking and recurring sheet asks for the vertical layout, with or without the customer`() {
        listOf(false, true).forEach { saveCard ->
            val configuration = INTENT.toPaymentSheetParams(saveCard = saveCard, currencyCode = "CZK").toConfiguration()
            // The layout is internal to Stripe's module; the configuration's own toString names it.
            assertTrue("$configuration", configuration.toString().contains("paymentMethodLayout=Vertical"))
        }
    }

    @Test
    fun `every sheet the app configures asks for the vertical layout`() {
        val main = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
            .map { File(it, "src/main/java/cz/cleansia/customer") }.first { it.isDirectory }
        val sites = main.walkTopDown().filter { it.isFile && it.extension == "kt" }
            .map { it.name to it.readText().replace(COMMENTS, "") }
            .filter { (_, code) -> code.contains("PaymentSheet.Configuration") }
            .toList()
        assertEquals(listOf("PaymentSheetParams.kt", "SubscribePlusScreen.kt"), sites.map { it.first }.sorted())
        sites.forEach { (name, code) ->
            assertFalse("$name constructs the configuration without the builder", CONSTRUCTOR.containsMatchIn(code))
            assertEquals("$name builds one configuration", 1, BUILDER.findAll(code).count())
            assertEquals("$name does not ask for the vertical layout", 1, VERTICAL.findAll(code).count())
        }
    }

    private companion object {
        val INTENT = CreatePaymentIntentResponse(
            clientSecret = "pi_secret",
            paymentIntentId = "pi_1",
            stripeCustomerId = "cus_1",
            ephemeralKey = "ek_1",
        )
        val COMMENTS = Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*""")
        val CONSTRUCTOR = Regex("""PaymentSheet\.Configuration\(""")
        val BUILDER = Regex("""PaymentSheet\.Configuration\.Builder\(""")
        val VERTICAL = Regex("""\.paymentMethodLayout\(\s*PaymentSheet\.PaymentMethodLayout\.Vertical\s*\)""")
    }
}
