package cz.cleansia.partner.features.orders

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.graphics.luminance
import cz.cleansia.partner.ui.theme.DarkColors
import cz.cleansia.partner.ui.theme.LightColors
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pins the payment-status colour buckets to the backend's numeric ordinals.
 *
 * The shipping pill bucketed off `Code.name` with substring matches, which got
 * two of the six statuses wrong: "Refunded" matched `contains("refund")` and
 * painted red, and "Disputed" matched nothing and fell through to neutral —
 * exactly backwards. The label functions are @Composable and untestable without
 * Compose test infrastructure, so `severity` is deliberately a plain function
 * and carries the whole assertion surface.
 */
class PaymentPresentationTest {

    @Test
    fun `paid is the happy path`() {
        assertEquals(PaymentSeverity.Success, PaymentPresentation.severity(2))
    }

    @Test
    fun `pending warns`() {
        assertEquals(PaymentSeverity.Warning, PaymentPresentation.severity(1))
    }

    @Test
    fun `failed and disputed both need the cleaner's attention`() {
        assertEquals(PaymentSeverity.Error, PaymentPresentation.severity(3))
        assertEquals(PaymentSeverity.Error, PaymentPresentation.severity(5))
    }

    @Test
    fun `refunds are neutral, not errors`() {
        assertEquals(PaymentSeverity.Neutral, PaymentPresentation.severity(4))
        assertEquals(PaymentSeverity.Neutral, PaymentPresentation.severity(6))
    }

    @Test
    fun `an absent or unknown ordinal stays neutral`() {
        assertEquals(PaymentSeverity.Neutral, PaymentPresentation.severity(null))
        assertEquals(PaymentSeverity.Neutral, PaymentPresentation.severity(0))
        assertEquals(PaymentSeverity.Neutral, PaymentPresentation.severity(99))
    }

    /**
     * The pill writes its label on a 12 % wash of the same colour over the card (Z2): the fixed amber-600 read
     * 2.81:1 there in light mode, the green-600 2.89:1, and in dark mode the green and the red 3.78 and 2.85:1.
     */
    @Test
    fun `every payment pill's label reads 4_5 to 1 on its own wash in both schemes`() {
        listOf("light" to LightColors, "dark" to DarkColors).forEach { (name, scheme) ->
            PaymentSeverity.entries.forEach { severity ->
                val ink = paymentStatusInk(severity, scheme)
                val ratio = contrast(ink, ink.copy(alpha = 0.12f).compositeOver(scheme.surface))
                assertTrue("$name $severity: ${"%.2f".format(ratio)}:1", ratio >= 4.5)
            }
        }
    }

    @Test
    fun `pending is amber-800 in light mode and amber-500 in dark, settled green-800 and green-400`() {
        assertEquals(Color(0xFF92400E), paymentStatusInk(PaymentSeverity.Warning, LightColors))
        assertEquals(Color(0xFFF59E0B), paymentStatusInk(PaymentSeverity.Warning, DarkColors))
        assertEquals(Color(0xFF166534), paymentStatusInk(PaymentSeverity.Success, LightColors))
        assertEquals(Color(0xFF4ADE80), paymentStatusInk(PaymentSeverity.Success, DarkColors))
        val amber600 = Color(0xFFD97706)
        assertTrue(contrast(amber600, amber600.copy(alpha = 0.12f).compositeOver(Color.White)) < 4.5)
    }

    private fun contrast(a: Color, b: Color): Double {
        val (hi, lo) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (hi + 0.05) / (lo + 0.05)
    }
}
