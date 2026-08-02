package cz.cleansia.customer.features.orders

import cz.cleansia.customer.R
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The order-summary payment row handled ordinals 1–5 and fell through to
 * `paymentStatus.name` — the backend's English enum text — for anything else.
 * `PartiallyRefunded = 6` exists today and is even named in the doc comment
 * above the `when`, so a partially refunded customer read "PartiallyRefunded"
 * in Czech, Slovak, Ukrainian or Russian. The partner app already resolves all
 * six ordinals (`PaymentPresentation`); this is the customer twin.
 */
class PaymentLabelTest {

    private val statusLabels = mapOf(
        1 to R.string.orders_payment_pending,
        2 to R.string.orders_payment_paid,
        3 to R.string.orders_payment_failed,
        4 to R.string.orders_payment_refunded,
        5 to R.string.orders_payment_disputed,
        6 to R.string.orders_payment_partially_refunded,
    )

    @Test
    fun `every backend payment status resolves to a translated label`() {
        statusLabels.forEach { (ordinal, res) ->
            assertEquals("ordinal $ordinal", res, paymentStatusLabelRes(ordinal))
        }
    }

    /** The ordinal the doc comment named and the `when` never handled. */
    @Test
    fun `partially refunded is one of them`() {
        assertEquals(R.string.orders_payment_partially_refunded, paymentStatusLabelRes(6))
    }

    @Test
    fun `no two statuses share a label`() {
        assertEquals(statusLabels.size, statusLabels.values.toSet().size)
    }

    @Test
    fun `both payment methods resolve to a translated label`() {
        assertEquals(R.string.booking_pay_cash, paymentMethodLabelRes(1))
        assertEquals(R.string.booking_pay_card, paymentMethodLabelRes(2))
    }

    @Test
    fun `an unmapped ordinal has no label resource`() {
        listOf(null, 0, 7, 99).forEach { ordinal ->
            assertNull("status $ordinal", paymentStatusLabelRes(ordinal))
            assertNull("method $ordinal", paymentMethodLabelRes(ordinal))
        }
    }

    /**
     * The defect: an ordinal added by the backend after this build shipped must
     * not put a wire name on screen. Production hides the row; a debug build
     * shows the bare ordinal so the gap surfaces to us instead.
     */
    @Test
    fun `an unknown ordinal shows nothing in production`() {
        assertNull(unknownPaymentLabel(7, isDebug = false))
        assertNull(unknownPaymentLabel(null, isDebug = false))
    }

    @Test
    fun `an unknown ordinal is a bare diagnostic in debug`() {
        assertEquals("#7", unknownPaymentLabel(7, isDebug = true))
        assertNull(unknownPaymentLabel(null, isDebug = true))
    }

    @Test
    fun `the partially refunded copy is translated in all five locales`() {
        val english = valueOf("values", "orders_payment_partially_refunded")
        listOf("values-cs", "values-sk", "values-uk", "values-ru").forEach { locale ->
            val translated = valueOf(locale, "orders_payment_partially_refunded")
            assertTrue(
                "$locale still carries the English wording: $translated",
                translated != english,
            )
        }
    }

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    private fun valueOf(locale: String, key: String): String {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(file.readText())
            ?.groupValues
            ?.get(1)
            ?: fail("no <string name=\"$key\"> in $locale/strings.xml").let { "" }
    }
}
