package cz.cleansia.customer.features.recurring

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A customer whose Plus membership lapsed keeps being charged for the cleanings
 * their schedules generate, so every route to the stop button has to survive the
 * lapse: the profile row, the home section, and the list screen's own content.
 * The gate belongs on the create and edit affordances alone, which is where the
 * server puts it.
 *
 * This module has no Compose test harness, so — as with `OrderDetailFooterTintTest`
 * — these read the source. A composable that hides a screen renders nothing and
 * fails nothing; the VM suite beside this one stays green either way.
 */
class RecurringManagementReachabilityTest {

    private companion object {
        val MEMBERSHIP_TOKENS = Regex("""isPlus|hasMembership|MembershipRepository|MembershipEntryPoint""")
    }

    @Test
    fun `the profile entry row is not gated on membership`() {
        val source = read("features/profile/RecurringEntryRow.kt")

        assertNull(
            "the recurring entry row reads membership again",
            MEMBERSHIP_TOKENS.find(source)?.value,
        )
        assertTrue(
            "ProfileTab no longer renders the recurring entry row",
            read("features/profile/ProfileTab.kt").contains("RecurringEntryRow("),
        )
    }

    @Test
    fun `the home section shows existing schedules regardless of membership`() {
        val source = read("features/home/HomeTab.kt")

        assertEquals(
            "val showRecurringSection = activeRecurring.isNotEmpty()",
            Regex("""val showRecurringSection = .*""").find(source)?.value,
        )
        assertTrue(
            "a lapsed member's templates never load, so the section cannot appear",
            Regex("""LaunchedEffect\(Unit\) \{\s*recurringRepo\.refresh\(\)""").containsMatchIn(source),
        )
    }

    @Test
    fun `the list screen branches on the empty state, never on membership`() {
        assertEquals(
            listOf("loading && !loaded", "affordances.showPlusUpsell", "templates.isEmpty()", "else"),
            contentWhenConditions(),
        )
    }

    @Test
    fun `the screen resolves its affordances from the gate the view model exposes`() {
        assertTrue(
            listScreen.contains("RecurringListAffordances.of(authoring, templates.isNotEmpty())"),
        )
    }

    @Test
    fun `the create affordance is the thing behind the gate`() {
        assertTrue(
            "the create affordance is not gated on the authoring decision",
            block("if (affordances.showCreateAction) {").contains("ExtendedFloatingActionButton"),
        )
    }

    @Test
    fun `pause and delete are wired unconditionally inside the list`() {
        val body = block("private fun TemplateList(")

        assertTrue("pause/resume is not wired", body.contains("onToggleActive = "))
        assertTrue("delete is not wired", body.contains("onDelete = "))
        assertTrue("the edit gate is applied to the card, not the list", body.contains("showEdit = showEdit"))
        assertNull(
            "a membership term guards the list of schedules",
            Regex("""if \([^)]*(?:showEdit|authoring|isPlus|hasMembership|Upsell)""").find(body)?.value,
        )
    }

    private val listScreen: String = read("features/recurring/RecurringBookingsScreen.kt")

    private fun read(relativePath: String): String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/$relativePath") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("$relativePath not found from working dir ${File(".").absolutePath}")

    /**
     * Conditions of the one `when` in the list screen, in order. Each arm's
     * condition is the last line before its `->` at brace depth zero, so an arm
     * added anywhere in the block changes this list.
     */
    private fun contentWhenConditions(): List<String> {
        val body = block("when {")
        val conditions = mutableListOf<String>()
        val buffer = StringBuilder()
        var depth = 0
        var index = body.indexOf('{') + 1
        while (index < body.length - 1) {
            val char = body[index]
            when (char) {
                '{', '(', '[' -> depth++
                '}', ')', ']' -> depth--
            }
            if (depth == 0 && char == '-' && body.getOrNull(index + 1) == '>') {
                conditions += buffer.toString().substringAfterLast('\n').trim()
                buffer.clear()
                index += 2
                continue
            }
            buffer.append(char)
            index++
        }
        return conditions
    }

    private fun block(marker: String): String {
        val start = listScreen.indexOf(marker)
        assertTrue("no `$marker` in RecurringBookingsScreen.kt", start >= 0)
        assertEquals(
            "`$marker` must be unique for this extraction to mean anything",
            start,
            listScreen.lastIndexOf(marker),
        )
        val open = listScreen.indexOf('{', start)
        var depth = 0
        for (i in open until listScreen.length) {
            when (listScreen[i]) {
                '{' -> depth++
                '}' -> if (--depth == 0) return listScreen.substring(open, i + 1)
            }
        }
        error("unbalanced braces after `$marker`")
    }
}
