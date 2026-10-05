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
 * An order's status pill writes its label in the status colour on a wash of the same colour over the card —
 * 14 % on the orders list and Home's recent booking, 16 % on the order's header. Every status reads 4.5:1 or
 * more there in both schemes (F1): "Pending" and "In progress" were 1.9:1 in light mode, "Confirmed" 3.4:1,
 * and "Completed" and "Cancelled" under 3:1 in dark mode.
 */
class OrderStatusInkTest {

    private val statuses = listOf(0, 1, 2, 3, 4, 5, 6, null, 99)

    @Test
    fun `every order status pill reads 4_5 to 1 on its wash over the card in light and dark mode`() {
        listOf("light" to LightColors, "dark" to DarkColors).forEach { (name, scheme) ->
            statuses.forEach { status ->
                val ink = orderStatusInk(status, scheme)
                listOf(0.14f, 0.16f).forEach { alpha ->
                    val ratio = contrast(ink, ink.copy(alpha = alpha).compositeOver(scheme.surface))
                    assertTrue("$name status $status at $alpha: ${"%.2f".format(ratio)}:1", ratio >= 4.5)
                }
            }
        }
    }

    @Test
    fun `confirmed takes the text blue, and in progress a different blue in each scheme`() {
        assertEquals(Color(0xFF0369A1), orderStatusInk(2, LightColors))
        assertEquals(DarkColors.primary, orderStatusInk(2, DarkColors))
        listOf(LightColors, DarkColors).forEach { scheme ->
            assertTrue(orderStatusInk(3, scheme) != orderStatusInk(2, scheme))
            assertEquals(orderStatusInk(3, scheme), orderStatusInk(4, scheme))
        }
    }

    private fun contrast(a: Color, b: Color): Double {
        val (hi, lo) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (hi + 0.05) / (lo + 0.05)
    }
}
