package cz.cleansia.customer.core.payments

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class StripeConfigTest {
    @Test
    fun `availability comes from the injected key`() {
        listOf("", " \t\n").forEach { assertEquals(false, StripeConfig(it).isCardPaymentAvailable) }
        val config = StripeConfig("pk_test_synthetic")
        assertEquals(true, config.isCardPaymentAvailable)
        assertEquals("pk_test_synthetic", config.publishableKey)
    }

    @Test
    fun `the module is the only production build key reader and all consumers use the same seam`() {
        val main = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
            .map { File(it, "src/main/java/cz/cleansia/customer") }.first { it.isDirectory }
        fun code(path: String) = File(main, path).readText().replace(COMMENTS, "")
        val readers = main.walkTopDown().filter { it.isFile && it.extension == "kt" }
            .flatMap { file -> KEY_READ.findAll(file.readText().replace(COMMENTS, "")).map { file.name } }.toList()
        assertEquals(listOf("PaymentModule.kt"), readers)
        assertTrue(code("core/payments/PaymentModule.kt").contains("StripeConfig(BuildConfig.STRIPE_PUBLISHABLE_KEY)"))
        val app = code("CleansiaApp.kt")
        assertTrue(app.contains("@Inject lateinit var stripeConfig: StripeConfig"))
        assertTrue(app.contains("if (stripeConfig.isCardPaymentAvailable)"))
        assertTrue(app.contains("PaymentConfiguration.init(this, stripeConfig.publishableKey)"))
        listOf("booking/ConfirmStep", "orders/OrderDetail", "membership/Membership", "recurring/CreateRecurring").forEach { feature ->
            assertTrue(code("features/${feature}ViewModel.kt").contains("val cardPaymentAvailable = stripeConfig.isCardPaymentAvailable"))
        }
        assertTrue(code("features/booking/ConfirmStep.kt").contains("val cardPaymentAvailable = viewModel.cardPaymentAvailable"))
        assertTrue(code("features/membership/SubscribePlusScreen.kt").contains("val canSubscribe = viewModel.cardPaymentAvailable"))
        val order = code("features/orders/OrderDetailScreen.kt")
        assertTrue(order.contains("cardPaymentAvailable = viewModel.cardPaymentAvailable"))
        assertEquals(2, Regex("cardPaymentAvailable: Boolean").findAll(order).count())
        assertTrue(order.contains("cardPaymentAvailable = cardPaymentAvailable"))
        val recurring = code("features/recurring/CreateRecurringScreen.kt")
        assertTrue(recurring.contains("cardPaymentAvailable = viewModel.cardPaymentAvailable"))
        assertTrue(recurring.contains("private fun PaymentTypePicker(selected: Int?, cashEnabled: Boolean, cardPaymentAvailable: Boolean,"))
    }

    private companion object {
        val COMMENTS = Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*""")
        val KEY_READ = Regex("""BuildConfig\.STRIPE_PUBLISHABLE_KEY""")
    }
}
