package cz.cleansia.partner.features.main

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The partner bar is the customer bar's second copy — its own private
 * `NavSlot` under a different package — so it carried the same wrap defect,
 * and its worst label is longer still (`dashboard` is "Панель керування" in
 * uk). `FloatingIslandBottomBar` is one `@Composable` with no seam and this
 * module has no Compose test harness, so these read the source directly.
 *
 * `maxLines` on its own would not have been the fix. A `Row` measures each
 * unweighted child against the space its earlier siblings left, so the last
 * slot is handed whatever remains — nothing, once a 16-character first label
 * has been paid for. The weight is what gives all four slots the same budget,
 * so the ellipsis lands somewhere a reader can use.
 */
class BottomNavLabelTruncationTest {

    @Test
    fun `the nav label renders on one line with a tail ellipsis`() {
        val label = labelText()
        assertTrue(
            "the bottom-nav label must pin maxLines = 1 explicitly — a wrapped " +
                "label overflows the pill's fixed 64dp height",
            label.contains("maxLines = 1"),
        )
        assertTrue(
            "the bottom-nav label must pin overflow = TextOverflow.Ellipsis explicitly; " +
                "clipping mid-glyph reads as a rendering fault, not as truncation",
            label.contains("overflow = TextOverflow.Ellipsis"),
        )
    }

    @Test
    fun `every nav slot takes an equal share of the pill`() {
        val callSites = navSlotCallSites()
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
    fun `the label string reaches the Text whole so TalkBack still reads it`() {
        val label = labelText()
        assertTrue(
            "the label must stay a bare stringResource — Compose reports the string it was " +
                "handed to the semantics tree, so visual truncation costs nothing, but a " +
                "Kotlin-side chop would truncate the spoken label too",
            label.contains("stringResource(labelRes)"),
        )
        val choppers = listOf(".take(", ".substring(", ".dropLast(", ".trimEnd(")
        val used = choppers.filter { navSlotBody().contains(it) }
        if (used.isNotEmpty()) {
            fail("the label must not be shortened in Kotlin: ${used.joinToString()}")
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

    private fun navSlotBody(): String = braceBlock(source, "private fun NavSlot(")

    private fun labelText(): String {
        val body = navSlotBody()
        val open = body.indexOf("Text(")
        assertTrue("no Text(...) inside NavSlot", open >= 0)
        return parenBlock(body, open + "Text".length)
    }

    private fun navSlotCallSites(): List<String> =
        Regex("""(?<!fun )NavSlot\(""").findAll(source)
            .map { parenBlock(source, it.range.last) }
            .toList()

    private fun braceBlock(text: String, marker: String): String {
        val start = text.indexOf(marker)
        assertTrue("no `$marker` in the source", start >= 0)
        assertTrue(
            "`$marker` must be unique for this extraction to mean anything",
            start == text.lastIndexOf(marker),
        )
        return matched(text, text.indexOf('{', start), '{', '}')
    }

    private fun parenBlock(text: String, open: Int): String = matched(text, open, '(', ')')

    private fun matched(text: String, open: Int, opener: Char, closer: Char): String {
        assertTrue("no `$opener` to match", open >= 0)
        var depth = 0
        for (i in open until text.length) {
            when (text[i]) {
                opener -> depth++
                closer -> if (--depth == 0) return text.substring(open, i + 1)
            }
        }
        error("unbalanced `$opener` from index $open")
    }
}
