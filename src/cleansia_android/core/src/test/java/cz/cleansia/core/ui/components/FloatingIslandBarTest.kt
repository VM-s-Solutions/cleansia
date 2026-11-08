package cz.cleansia.core.ui.components

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The bottom-bar labels wrapped to a second line in uk/ru and spilled out of the fixed 64dp pill, in both apps, when
 * each drew its own copy of the slot. This module has no Compose test harness, so these read the source directly; each
 * app's `BottomNavLabelTruncationTest` checks that its slots take equal shares of the pill.
 */
class FloatingIslandBarTest {

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
    fun `the label string reaches the Text whole so TalkBack still reads it`() {
        assertTrue(
            "the slot must hand the Text the label it was given — Compose reports the string it was " +
                "handed to the semantics tree, so visual truncation costs nothing, but a " +
                "Kotlin-side chop would truncate the spoken label too",
            labelText().contains("text = label,"),
        )
        val choppers = listOf(".take(", ".substring(", ".dropLast(", ".trimEnd(")
        val used = choppers.filter { slotBody().contains(it) }
        if (used.isNotEmpty()) {
            fail("the label must not be shortened in Kotlin: ${used.joinToString()}")
        }
    }

    /**
     * A custom tab row declares its selection itself, as Material's NavigationBar does: TalkBack reports
     * the selected flag only for `Role.Tab`, and `clickable` gives a slot an action but no state.
     */
    @Test
    fun `each slot reports itself as a tab and whether it is the selected one`() {
        val slot = slotBody()
        val at = slot.indexOf(".selectable(")
        assertTrue("the slot must declare itself with Modifier.selectable(...)", at >= 0)
        val selectable = matched(slot, at + ".selectable".length, '(', ')')
        listOf("selected = selected", "indication = null", "role = Role.Tab").forEach { argument ->
            assertTrue("the slot's selectable(...) must pass `$argument`", selectable.contains(argument))
        }
        assertTrue("the slot must not fall back to clickable, which carries no selection", !slot.contains(".clickable("))
        assertTrue(
            "the bar's row must group its tabs with selectableGroup()",
            functionBody("fun FloatingIslandBar(").contains(".selectableGroup()"),
        )
    }

    private val source: String = sequenceOf(
        File("."),
        File("core"),
        File("src/cleansia_android/core"),
    ).map { File(it, "src/main/java/cz/cleansia/core/ui/components/FloatingIslandBar.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("FloatingIslandBar.kt not found from working dir ${File(".").absolutePath}")

    private fun slotBody(): String = functionBody("fun FloatingIslandSlot(")

    private fun labelText(): String {
        val body = slotBody()
        val open = body.indexOf("Text(")
        assertTrue("no Text(...) inside FloatingIslandSlot", open >= 0)
        return matched(body, open + "Text".length, '(', ')')
    }

    /** The body after the signature's parameter list, whose default lambdas would otherwise read as the body. */
    private fun functionBody(marker: String): String {
        val start = source.indexOf(marker)
        assertTrue("no `$marker` in the source", start >= 0)
        assertTrue("`$marker` must be unique for this extraction to mean anything", start == source.lastIndexOf(marker))
        val params = matched(source, start + marker.length - 1, '(', ')')
        return matched(source, source.indexOf('{', start + marker.length - 1 + params.length), '{', '}')
    }

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
