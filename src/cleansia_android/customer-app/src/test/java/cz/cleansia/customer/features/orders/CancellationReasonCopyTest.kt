package cz.cleansia.customer.features.orders

import cz.cleansia.customer.R
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The server writes four platform cancellation reasons and the detail screen turned three of
 * them into a sentence. The fourth, written by the unfilled-order sweep, reached the customer
 * as a cancelled order with no line saying why — the one no-show the platform can prove, and
 * the one that pays the apology credit, was the one left silent.
 */
class CancellationReasonCopyTest {

    /** Mirrors `Cleansia.Core.Domain.Orders.OrderCancellationReasons`. */
    private val reasons = mapOf(
        "order.cancelled.payment_not_completed" to R.string.order_cancelled_reason_payment_not_completed,
        "order.cancelled.recurring_not_confirmed" to R.string.order_cancelled_reason_recurring_not_confirmed,
        "order.cancelled.company_wind_down" to R.string.order_cancelled_reason_company_wind_down,
        "order.cancelled.no_cleaner_available" to R.string.order_cancelled_reason_no_cleaner_available,
    )

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /**
     * The refund and the apology credit are each a line of their own on the detail, so the
     * reason may not restate them — a second statement of the same money fact can drift.
     */
    private val moneyStems = mapOf(
        "values" to listOf("refund", "credit", "paid"),
        "values-cs" to listOf("vrac", "vrát", "kredit", "zaplat"),
        "values-sk" to listOf("vrac", "vrát", "kredit", "zaplat"),
        "values-uk" to listOf("поверн", "кредит", "бонус", "сплат"),
        "values-ru" to listOf("возвр", "верн", "кредит", "бонус", "оплат"),
    )

    @Test
    fun `every platform reason resolves to a translated sentence`() {
        reasons.forEach { (reason, res) ->
            assertEquals(reason, res, cancellationReasonRes(reason))
        }
    }

    @Test
    fun `the unfilled-order sweep's reason is one of them`() {
        assertEquals(
            R.string.order_cancelled_reason_no_cleaner_available,
            cancellationReasonRes("order.cancelled.no_cleaner_available"),
        )
    }

    @Test
    fun `no two reasons share a sentence`() {
        assertEquals(reasons.size, reasons.values.toSet().size)
    }

    /** A newer server's key must not put `order.cancelled.something` on screen. */
    @Test
    fun `an unknown reason, a blank one and none at all render nothing`() {
        listOf(null, "", "order.cancelled.something_newer", "free text from an admin").forEach { reason ->
            assertNull("reason $reason", cancellationReasonRes(reason))
        }
    }

    @Test
    fun `the no-cleaner sentence is translated in all five locales`() {
        val english = valueOf("values", "order_cancelled_reason_no_cleaner_available")
        assertTrue(english.isNotBlank())
        locales.drop(1).forEach { locale ->
            val translated = valueOf(locale, "order_cancelled_reason_no_cleaner_available")
            assertTrue("$locale/strings.xml has a blank sentence", translated.isNotBlank())
            assertTrue("$locale/strings.xml left the sentence in English", translated != english)
        }
    }

    @Test
    fun `the no-cleaner sentence leaves the refund and the credit to their own lines`() {
        locales.forEach { locale ->
            val sentence = valueOf(locale, "order_cancelled_reason_no_cleaner_available").lowercase()
            moneyStems.getValue(locale).forEach { stem ->
                assertFalse("$locale/strings.xml states money ($stem) in the reason", sentence.contains(stem))
            }
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
