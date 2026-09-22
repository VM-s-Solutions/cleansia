package cz.cleansia.customer.features.disputes

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The dispute card carries its status in the pill only; the left accent strip is gone by owner
 * ruling. A Compose screen in this module has no test harness, so the card body is pinned as source
 * text scoped to the one function.
 */
class DisputesListCardTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res/values/strings.xml").isFile }
        ?: error("customer-app module not found from working dir ${File(".").absolutePath}")

    private val disputeRow: String by lazy {
        val file = File(moduleDir, "src/main/java/cz/cleansia/customer/features/disputes/DisputesListScreen.kt")
        assertTrue("${file.absolutePath} not found", file.isFile)
        functionBody(file.readText(), "private fun DisputeRow(")
    }

    /** The braces of `signature` … `}`, so a sibling composable cannot leak in. */
    private fun functionBody(source: String, signature: String): String {
        val start = source.indexOf(signature)
        assertTrue("`$signature` no longer appears — this parser is stale", start >= 0)
        val open = source.indexOf('{', source.indexOf(')', start))
        var depth = 0
        for (index in open until source.length) {
            when (source[index]) {
                '{' -> depth++
                '}' -> {
                    depth--
                    if (depth == 0) return source.substring(open, index + 1)
                }
            }
        }
        error("unbalanced braces after `$signature`")
    }

    @Test
    fun `the card paints no left accent strip`() {
        assertFalse("the card still stretches to fit a strip: IntrinsicSize", disputeRow.contains("IntrinsicSize"))
        assertFalse("the card still draws a full-height strip", disputeRow.contains("fillMaxHeight"))
        assertFalse("the card still reserves a 4dp column", disputeRow.contains("width(4.dp)"))
    }

    @Test
    fun `the status colour survives on the pill`() {
        assertTrue(disputeRow.contains("StatusPill("))
        assertTrue(disputeRow.contains("color = statusColor"))
    }
}
