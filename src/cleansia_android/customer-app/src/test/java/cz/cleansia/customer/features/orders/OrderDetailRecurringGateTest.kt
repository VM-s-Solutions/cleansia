package cz.cleansia.customer.features.orders

import java.io.File
import org.junit.Assert.assertEquals
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

    private val screen: String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("OrderDetailScreen.kt not found from working dir ${File(".").absolutePath}")
}
