package cz.cleansia.partner.ui.theme

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.graphics.luminance
import cz.cleansia.core.ui.theme.orderStatusInk
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The core order status pill writes its label in the status ink on a 16 % wash of the same ink over the card. Every
 * status reads 4.5:1 or more there over this app's own schemes too, not only over the customer app's.
 */
class OrderStatusInkTest {

    private val statuses = listOf(0, 1, 2, 3, 4, 5, 6, null, 99)

    @Test
    fun `every order status pill reads 4_5 to 1 on its wash over the card in light and dark mode`() {
        listOf("light" to LightColors, "dark" to DarkColors).forEach { (name, scheme) ->
            statuses.forEach { status ->
                val ink = orderStatusInk(status, scheme)
                val ratio = contrast(ink, ink.copy(alpha = 0.16f).compositeOver(scheme.surface))
                assertTrue("$name status $status: ${"%.2f".format(ratio)}:1", ratio >= 4.5)
            }
        }
    }

    private fun contrast(a: Color, b: Color): Double {
        val (hi, lo) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (hi + 0.05) / (lo + 0.05)
    }
}
