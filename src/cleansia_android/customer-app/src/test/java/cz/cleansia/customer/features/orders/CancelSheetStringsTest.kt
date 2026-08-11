package cz.cleansia.customer.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The cancel sheet's fee copy is a statement about the customer's money, and it
 * was wrong in five languages at once. There is no `androidTest` source set on
 * this module, so nothing else here can see a missing or reverted translation.
 */
class CancelSheetStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val keys = listOf(
        "order_cancel_fee_not_accepted",
        "order_cancel_fee_oops",
        "order_cancel_fee_outside_window",
        "order_cancel_fee_partial",
        "order_cancel_fee_last_minute",
        "order_cancel_fee_none",
        "order_cancel_fee_split",
        "order_cancel_fee_checking",
        "order_cancel_fee_unavailable",
        "order_cancel_fee_retry",
        "order_cancel_fee_recheck_note",
        "order_cancel_express_waiver_forfeit",
    )

    /** The client-side ladder's copy. Each one stated a rate the server never charged. */
    private val retired = listOf(
        "order_cancel_fee_free",
        "order_cancel_fee_50",
        "order_cancel_fee_100",
        "order_cancel_fee_estimate_note",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every cancel-fee string is translated in all five locales`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                val value = valueOf(xml, key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
            }
        }
    }

    @Test
    fun `the four translations are not the English string copied over`() {
        val english = keys.associateWith { valueOf(stringsXml("values"), it) }
        locales.drop(1).forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                assertTrue(
                    "$locale/strings.xml left $key in English",
                    valueOf(xml, key) != english[key],
                )
            }
        }
    }

    @Test
    fun `the copy of the client-side ladder is gone from every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            retired.forEach { key ->
                assertEquals("$locale/strings.xml still carries $key", null, valueOf(xml, key))
            }
        }
    }

    @Test
    fun `no cancel-fee string quotes a rate of its own`() {
        locales.forEach { locale ->
            Regex(
                "<string name=\"(order_cancel_fee[^\"]*)\"[^>]*>(.*?)</string>",
                RegexOption.DOT_MATCHES_ALL,
            ).findAll(stringsXml(locale)).forEach { match ->
                val (key, value) = match.destructured
                // "%%" is an escaped literal percent sign — the fingerprint of a
                // hardcoded tier rate. The fee and the refund arrive as money.
                assertFalse("$locale/strings.xml quotes a percentage in $key", value.contains("%%"))
            }
        }
    }

    @Test
    fun `the split line takes the fee and the refund, in that order`() {
        locales.forEach { locale ->
            val value = valueOf(stringsXml(locale), "order_cancel_fee_split")!!
            assertTrue("$locale/strings.xml dropped the fee from the split line", value.contains("%1\$s"))
            assertTrue("$locale/strings.xml dropped the refund from the split line", value.contains("%2\$s"))
            assertTrue(
                "$locale/strings.xml puts the refund before the fee",
                value.indexOf("%1\$s") < value.indexOf("%2\$s"),
            )
        }
    }

    @Test
    fun `the express-waiver warning never spells out the quota`() {
        // The number of included express bookings is per-plan configurable, so a
        // literal in the copy is a promise the plan can break.
        locales.forEach { locale ->
            val value = valueOf(stringsXml(locale), "order_cancel_express_waiver_forfeit")!!
            assertFalse(
                "$locale/strings.xml hardcodes a quota in the express-waiver warning",
                value.any { it.isDigit() },
            )
        }
    }

    @Test
    fun `the sheet actually renders the express-waiver warning`() {
        // ADR-0035 AM-13 makes this disclosure mandatory, and the resolver test
        // that decides it cannot see whether the sheet draws it.
        val sheet = File(resDir.parentFile, "java/cz/cleansia/customer/features/orders/CancelOrderSheet.kt")
        assertTrue("CancelOrderSheet.kt not found next to res/", sheet.isFile)
        val source = sheet.readText()

        assertTrue(
            "the sheet no longer branches on the server's forfeiture flag",
            source.contains("callout.warnsExpressWaiverForfeited"),
        )
        assertTrue(
            "the sheet no longer renders the express-waiver warning",
            source.contains("R.string.order_cancel_express_waiver_forfeit"),
        )
    }

    private fun stringsXml(locale: String): String {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private fun valueOf(xml: String, key: String): String? =
        Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
            ?.groupValues
            ?.get(1)
}
