package cz.cleansia.customer.features.main

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The bottom-bar labels wrapped to a second line in uk/ru and spilled out of
 * the fixed 64dp pill. The slot is now `:core`'s `FloatingIslandSlot`, whose
 * label and tab semantics `FloatingIslandBarTest` checks; these check how
 * `MainShell` calls it. `MainShell` is one big `@Composable` with no seam and
 * this module has no Compose test harness, so — as with
 * `NotificationsScreenTogglesTest` — these read the source directly.
 *
 * `maxLines` on its own would not have been the fix. A `Row` measures each
 * unweighted child against the space its earlier siblings left, so the last
 * slot is handed whatever remains — and after three Cyrillic labels and the
 * 72dp FAB hole there is nothing left, which turns the wrap into a Profile tab
 * measured to zero width. The weight is what gives all four slots the same
 * budget, so the ellipsis lands somewhere a reader can use.
 */
class BottomNavLabelTruncationTest {

    @Test
    fun `every nav slot takes an equal share of the pill`() {
        val callSites = slotCallSites()
        assertEquals("expected the bar to still render four slots", 4, callSites.size)

        val unweighted = callSites.filterNot { it.contains("weight(1f)") }
        if (unweighted.isNotEmpty()) {
            fail(
                "Row hands each unweighted child only what its earlier siblings left, so " +
                    "these slots would be measured last and ellipsized to nothing in uk/ru: " +
                    unweighted.joinToString(" | "),
            )
        }
    }

    @Test
    fun `the label string reaches the slot whole so TalkBack still reads it`() {
        val callSites = slotCallSites()
        assertEquals("expected the bar to still render four slots", 4, callSites.size)
        callSites.forEach { call ->
            assertTrue(
                "the label must stay a bare stringResource — Compose reports the string it was " +
                    "handed to the semantics tree, so visual truncation costs nothing, but a " +
                    "Kotlin-side chop would truncate the spoken label too: $call",
                call.contains("label = stringResource("),
            )
            val used = listOf(".take(", ".substring(", ".dropLast(", ".trimEnd(").filter { call.contains(it) }
            if (used.isNotEmpty()) fail("the label must not be shortened in Kotlin: ${used.joinToString()}")
        }
    }

    private val source: String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/features/main/MainShell.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("MainShell.kt not found from working dir ${File(".").absolutePath}")

    private fun slotCallSites(): List<String> =
        Regex("""FloatingIslandSlot\(""").findAll(source)
            .map { matched(source, it.range.last) }
            .toList()

    private fun matched(text: String, open: Int): String {
        var depth = 0
        for (i in open until text.length) {
            when (text[i]) {
                '(' -> depth++
                ')' -> if (--depth == 0) return text.substring(open, i + 1)
            }
        }
        error("unbalanced `(` from index $open")
    }
}
