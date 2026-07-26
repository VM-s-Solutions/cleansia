package cz.cleansia.partner.features.orders

import org.junit.Assert.assertEquals
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
}
