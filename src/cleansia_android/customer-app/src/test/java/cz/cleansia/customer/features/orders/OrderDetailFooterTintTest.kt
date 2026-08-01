package cz.cleansia.customer.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The footer's colour roles. `ActionsFooter` is one `@Composable` with no seam
 * and this module has no Compose test harness, so — as with
 * `NotificationsScreenTogglesTest` — these read the source directly. Literal,
 * but it is the only thing standing between a role reassignment and silence:
 * a colour argument compiles whatever it is set to, and every existing test on
 * this screen passes either way.
 *
 * Each block is extracted by matching braces from its `if (...) {` gate, so a
 * token named anywhere else in the footer cannot satisfy an assertion here.
 */
class OrderDetailFooterTintTest {

    /**
     * Reporting destroys nothing, so it takes the error palette by owner
     * decision rather than by the usual destructive rule. Reverting it to
     * `primary` is the regression this exists to catch — the reported defect was
     * that it matched the Make recurring row directly above it.
     */
    @Test
    fun `report issue tints both slots with the error role and names no other`() {
        assertEquals(setOf("error"), roles("showReportIssue"))
        assertTrue(
            "expected a border AND a contentColor on the report-issue button",
            roleMentions("showReportIssue").size >= 2,
        )
    }

    @Test
    fun `make recurring keeps the brand primary it no longer shares with report issue`() {
        assertEquals(setOf("primary"), roles("showMakeRecurring"))
        assertNotEquals(roles("showMakeRecurring"), roles("showReportIssue"))
    }

    /**
     * Cancel and Report issue both render on Confirmed, one 8dp spacer apart, and
     * now carry the same tint — so their glyphs are the entire non-textual
     * differentiator between abandoning a booking and complaining about one.
     */
    @Test
    fun `cancel and report issue share a tint so their icons must differ`() {
        assertEquals(setOf("error"), roles("showCancel"))
        assertEquals(roles("showCancel"), roles("showReportIssue"))
        assertNotEquals(icon("showCancel"), icon("showReportIssue"))
    }

    /** A colour ticket must not quietly relabel the action. */
    @Test
    fun `report issue still reads from the label it always had`() {
        assertEquals(listOf("order_action_report_issue"), stringKeys("showReportIssue"))
    }

    private val source: String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("OrderDetailScreen.kt not found from working dir ${File(".").absolutePath}")

    private fun roles(gate: String): Set<String> = roleMentions(gate).toSet()

    private fun roleMentions(gate: String): List<String> =
        Regex("""colorScheme\.(\w+)""").findAll(block(gate)).map { it.groupValues[1] }.toList()

    private fun icon(gate: String): String =
        Regex("""Icons\.(?:\w+\.)*(\w+)""").find(block(gate))?.groupValues?.get(1)
            ?: error("no Icons.* reference inside `if ($gate)`")

    private fun stringKeys(gate: String): List<String> =
        Regex("""R\.string\.(\w+)""").findAll(block(gate)).map { it.groupValues[1] }.toList()

    private fun block(gate: String): String {
        val marker = "if ($gate) {"
        val start = source.indexOf(marker)
        assertTrue("no `$marker` in OrderDetailScreen.kt", start >= 0)
        assertTrue(
            "`$marker` must be unique for this extraction to mean anything",
            start == source.lastIndexOf(marker),
        )
        val open = start + marker.length - 1
        var depth = 0
        for (i in open until source.length) {
            when (source[i]) {
                '{' -> depth++
                '}' -> if (--depth == 0) return source.substring(open, i + 1)
            }
        }
        error("unbalanced braces after `$marker`")
    }
}
