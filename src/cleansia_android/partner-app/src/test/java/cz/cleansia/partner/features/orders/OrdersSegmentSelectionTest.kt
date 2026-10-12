package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The Orders segmented control drew its selection in colour, weight and shadow only, so TalkBack read three
 * buttons and never which one was on. As in the bottom bar, the row groups its segments and each segment is
 * a tab that reports whether it is the selected one: TalkBack reports the selected flag only for
 * `Role.Tab`. This module has no Compose test harness, so the control is read as source.
 */
class OrdersSegmentSelectionTest {

    @Test
    fun `the segments are tabs in a selectable group that report which one is selected`() {
        val tabs = braceBlock(source, "private fun SegmentedTabs(")
        assertTrue("the segments' row must group them with selectableGroup()", tabs.contains(".selectableGroup()"))
        val chip = braceBlock(source, "private fun SegmentChip(")
        val surfaceAt = chip.indexOf("Surface(")
        assertTrue("the segment lost its Surface", surfaceAt >= 0)
        val surface = parenBlock(chip, surfaceAt + "Surface".length)
        listOf("selected = selected", "onClick = onClick", "role = Role.Tab").forEach { argument ->
            assertTrue("the segment's Surface must carry `$argument`, got $surface", surface.contains(argument))
        }
    }

    private val source: String = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/orders/OrdersListScreen.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?.replace("\r\n", "\n")
        ?.replace(Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*"""), "")
        ?: error("OrdersListScreen.kt not found from working dir ${File(".").absolutePath}")

    private fun braceBlock(text: String, marker: String): String {
        val start = text.indexOf(marker)
        assertTrue("no `$marker` in OrdersListScreen.kt", start >= 0)
        assertTrue("`$marker` must be unique for this extraction to mean anything", start == text.lastIndexOf(marker))
        return matched(text, text.indexOf('{', start), '{', '}')
    }

    private fun parenBlock(text: String, open: Int): String = matched(text, open, '(', ')')

    private fun matched(text: String, open: Int, opener: Char, closer: Char): String {
        var depth = 0
        for (i in open until text.length) {
            when (text[i]) {
                opener -> depth++
                closer -> if (--depth == 0) return text.substring(open, i + 1)
            }
        }
        error("unbalanced `$opener` from offset $open")
    }
}
