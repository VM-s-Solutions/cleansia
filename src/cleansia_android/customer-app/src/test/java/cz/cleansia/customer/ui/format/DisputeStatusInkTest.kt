package cz.cleansia.customer.ui.format

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.graphics.luminance
import cz.cleansia.customer.ui.theme.DarkColors
import cz.cleansia.customer.ui.theme.LightColors
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A dispute's status pill writes its label in the status colour on a 14 % wash of the same colour, over the
 * card (the list row and the detail header are both `surface`). Every status reads 4.5:1 or more there in
 * both schemes (F-b): "Pending" was amber-500 on its own amber wash, 1.93:1 in light mode.
 */
class DisputeStatusInkTest {

    private val statuses = listOf(1, 2, 3, 4, 5, 6, null, 99)

    @Test
    fun `every status pill reads 4_5 to 1 on its wash over the card in light and dark mode`() {
        listOf("light" to LightColors, "dark" to DarkColors).forEach { (name, scheme) ->
            statuses.forEach { status ->
                val ink = disputeStatusInk(status, scheme)
                val ratio = contrast(ink, ink.copy(alpha = 0.14f).compositeOver(scheme.surface))
                assertTrue("$name status $status: ${"%.2f".format(ratio)}:1", ratio >= 4.5)
            }
        }
    }

    @Test
    fun `pending is a darker amber in light mode and keeps the brand amber in dark`() {
        assertEquals(Color(0xFF92400E), disputeStatusInk(1, LightColors))
        assertEquals(Color(0xFFF59E0B), disputeStatusInk(1, DarkColors))
    }

    @Test
    fun `the in-review pair takes the text blue and escalated the scheme's error`() {
        listOf(2, 3).forEach { assertEquals(Color(0xFF0369A1), disputeStatusInk(it, LightColors)) }
        listOf(2, 3).forEach { assertEquals(DarkColors.primary, disputeStatusInk(it, DarkColors)) }
        assertEquals(LightColors.error, disputeStatusInk(6, LightColors))
        assertEquals(DarkColors.error, disputeStatusInk(6, DarkColors))
    }

    private fun contrast(a: Color, b: Color): Double {
        val (hi, lo) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (hi + 0.05) / (lo + 0.05)
    }
}
