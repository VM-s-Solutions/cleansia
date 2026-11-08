package cz.cleansia.partner.features.main

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The partner bar was the customer bar's second copy — its own private
 * `NavSlot` under a different package — so it carried the same wrap defect,
 * and its worst label is longer still (`dashboard` is "Панель керування" in
 * uk). The slot is now `:core`'s `FloatingIslandSlot`, whose label and tab
 * semantics `FloatingIslandBarTest` checks; these check how this bar calls it.
 * This module has no Compose test harness, so they read the source directly.
 *
 * `maxLines` on its own would not have been the fix. A `Row` measures each
 * unweighted child against the space its earlier siblings left, so the last
 * slot is handed whatever remains — nothing, once a 16-character first label
 * has been paid for. The weight is what gives all four slots the same budget,
 * so the ellipsis lands somewhere a reader can use.
 */
class BottomNavLabelTruncationTest {

    @Test
    fun `every nav slot takes an equal share of the pill`() {
        val callSites = slotCallSites()
        assertTrue("expected the bar to still render slots", callSites.isNotEmpty())

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
        assertTrue("expected the bar to still render slots", callSites.isNotEmpty())
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
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).map { File(it, "src/main/java/cz/cleansia/partner/features/main/FloatingIslandBottomBar.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("FloatingIslandBottomBar.kt not found from working dir ${File(".").absolutePath}")

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
