package cz.cleansia.partner.ui.theme

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The invoice status badge (InvoiceStatusBadge) writes its label on a solid fill that is the same in both
 * schemes, so each pair has to read 4.5:1 on its own (F1): "Approved" was white on sky-600 (4.10:1) and
 * "Cancelled" slate-500 on slate-100 (4.34:1).
 */
class StatusPillContrastTest {

    private val pairs = mapOf(
        "pending" to (StatusPendingBg to StatusPendingText),
        "confirmed / approved" to (StatusConfirmedBg to StatusConfirmedText),
        "completed / paid" to (StatusCompletedBg to StatusCompletedText),
        "cancelled" to (StatusCancelledBg to StatusCancelledText),
        "disputed / rejected" to (StatusFailedBg to StatusFailedText),
    )

    @Test
    fun `every status pill's label reads 4_5 to 1 on its fill`() {
        pairs.forEach { (name, pair) ->
            val ratio = contrast(pair.second, pair.first)
            assertTrue("$name: ${"%.2f".format(ratio)}:1", ratio >= 4.5)
        }
    }

    @Test
    fun `approved is white on sky-700, cancelled slate-600 on slate-100`() {
        assertEquals(Sky700 to Color.White, StatusConfirmedBg to StatusConfirmedText)
        assertEquals(Slate100 to Slate600, StatusCancelledBg to StatusCancelledText)
    }

    private fun contrast(a: Color, b: Color): Double {
        val (hi, lo) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (hi + 0.05) / (lo + 0.05)
    }
}
