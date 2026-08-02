package cz.cleansia.partner.features.invoices

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The invoice detail was reported as having no back button. It had one, wired
 * to `popBackStack`; it was drawn underneath the system clock. The partner app
 * calls `enableEdgeToEdge()`, so a screen whose root is a plain
 * `Column(fillMaxSize)` starts at y=0 — behind the status bar — and its 8dp-
 * padded header row lands inside the status-bar strip.
 *
 * The screen is one `@Composable` with no seam and this module has no Compose
 * test harness, so — as with the customer app's `NotificationsScreenTogglesTest`
 * — these read the source directly. A missing inset costs nothing at compile
 * time and looks like a missing control at runtime, which is exactly the class
 * of defect nothing else here would catch.
 */
class InvoiceDetailInsetsTest {

    private val statusBarMechanism = "WindowInsets.statusBars.asPaddingValues().calculateTopPadding()"

    @Test
    fun `the header is pushed below the status bar before the back arrow is drawn`() {
        val inset = detail.indexOf("Spacer(Modifier.height(statusBarTop))")
        assertTrue(
            "the header must consume the status-bar inset, or the back arrow renders " +
                "behind the system clock under enableEdgeToEdge()",
            inset >= 0,
        )
        val backArrow = detail.indexOf("onClick = onNavigateBack")
        assertTrue("no back button on the invoice detail", backArrow >= 0)
        assertTrue(
            "the inset must be consumed above the header row, not below it",
            inset < backArrow,
        )
    }

    /**
     * Both invoice screens and `PeriodPayScreen` read the top inset the same
     * way. Keeping one mechanism per app is the point: divergence here is how
     * the detail screen came to skip it in the first place.
     */
    @Test
    fun `the detail reads the top inset the same way its sibling list does`() {
        assertTrue(
            "InvoicesListScreen uses `$statusBarMechanism` — if that changed, change both",
            list.contains(statusBarMechanism),
        )
        assertTrue(
            "InvoiceDetailScreen must read the top inset the same way its sibling does",
            detail.contains(statusBarMechanism),
        )
    }

    /**
     * The screen ends in the open-PDF button. Under `enableEdgeToEdge()` a bare
     * 24dp bottom padding leaves it under a 48dp three-button navigation bar.
     */
    @Test
    fun `the scrolling content clears the navigation bar at the bottom`() {
        assertTrue(
            "the trailing open-PDF button must clear the navigation bar",
            detail.contains("WindowInsets.navigationBars.asPaddingValues().calculateBottomPadding()"),
        )
        val applied = detail.indexOf("bottom = Spacing.L + navigationBarBottom")
        val button = detail.indexOf("CleansiaPrimaryButton(")
        assertTrue("the navigation-bar inset is read but never applied", applied >= 0)
        assertTrue("no open-PDF button on the invoice detail", button >= 0)
        assertTrue(
            "the inset must pad the scrolling content that holds the button",
            applied < button,
        )
    }

    private fun read(name: String): String = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).map { File(it, "src/main/java/cz/cleansia/partner/features/invoices/$name") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("$name not found from working dir ${File(".").absolutePath}")

    private val detail: String = read("InvoiceDetailScreen.kt")
    private val list: String = read("InvoicesListScreen.kt")
}
