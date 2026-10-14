package cz.cleansia.customer.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The screen read the membership cache directly and treated "not answered yet"
 * as "not a member", so the shortcut vanished for paid-up members on a cold
 * deep link. The view model suite proves the gate resolves permissively; this
 * proves the screen is what consumes it. A composable that hides one button
 * renders fine and fails nothing, and this module has no Compose test harness —
 * as with `OrderDetailFooterTintTest`, that leaves the source.
 */
class OrderDetailRecurringGateTest {

    @Test
    fun `the footer flag is derived from the gate the view model exposes`() {
        assertEquals(
            "val canMakeRecurring = canRebook && recurringAuthoring == RecurringAuthoringGate.Allowed",
            Regex("""val canMakeRecurring = .*""").find(screen)?.value,
        )
        assertTrue(
            "the gate is not collected from the view model",
            screen.contains("viewModel.recurringAuthoring.collectAsStateWithLifecycle()"),
        )
    }

    @Test
    fun `the screen reads no membership cache of its own`() {
        assertNull(
            "the screen reads membership again instead of the resolved gate",
            Regex("""membershipRepository|hasMembership""").find(screen)?.value,
        )
    }

    @Test
    fun `the derived flag is what the footer renders on`() {
        assertTrue(
            "the layout no longer receives the derived flag",
            screen.contains("showMakeRecurring = canMakeRecurring"),
        )
    }

    /**
     * A confirmed cash occurrence stays payment-Pending until the cleaner collects, so a button keyed
     * on Pending stayed up after the confirm and a second tap was refused.
     */
    @Test
    fun `the recurring confirm keys on the server's needsConfirmation, not the payment status`() {
        val flag = Regex("""val showConfirmRecurringCta = .*""").find(screen)?.value.orEmpty()
        assertTrue(
            "the confirm must key on the server's needsConfirmation, got `$flag`",
            flag.startsWith("val showConfirmRecurringCta = order.needsConfirmation"),
        )
        assertFalse("the confirm must not key on the payment status, got `$flag`", flag.contains("paymentStatus"))
        assertTrue(
            "the derived flag no longer gates the confirm button",
            screen.contains("if (showConfirmRecurringCta) {"),
        )
    }

    /**
     * As on iOS, a build without a Stripe key does not offer a card occurrence's confirm: the server spends
     * the customer's credit on it, and the screen then opens a PaymentSheet that cannot. A cash occurrence
     * takes no card, so it still confirms.
     */
    @Test
    fun `a card occurrence offers no confirm in a build without a Stripe key`() {
        assertTrue(
            "the screen must read card availability from the key that initialises Stripe",
            screen.contains("cardPaymentAvailable = viewModel.cardPaymentAvailable"),
        )
        assertEquals(
            "val payByCard = order.paymentType?.value == 2",
            Regex("""val payByCard = .*""").find(screen)?.value,
        )
        assertEquals(
            "val showConfirmRecurringCta = order.needsConfirmation && (cardPaymentAvailable || !payByCard)",
            Regex("""val showConfirmRecurringCta = .*""").find(screen)?.value,
        )
    }

    private val screen: String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("OrderDetailScreen.kt not found from working dir ${File(".").absolutePath}")
}
