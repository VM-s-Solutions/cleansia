package cz.cleansia.customer.features.recurring

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The upsell a non-member gets instead of the create affordance, and the notice
 * telling a lapsed member their schedules keep running at the full non-member
 * price. Android falls back to `values/` for a missing locale row and this module
 * has no lint step in CI, so a dropped row ships silently — on copy that states
 * what a customer is being charged.
 */
class RecurringPlusGateStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val keys = listOf(
        "recurring_plus_gate_title",
        "recurring_plus_gate_subtitle",
        "recurring_plus_gate_cta",
        "recurring_lapsed_notice_title",
        "recurring_lapsed_notice_body",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    private fun stringsXml(locale: String): String {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    @Test
    fun `plus gate and lapsed notice are present in all five locales`() {
        val missing = mutableListOf<String>()
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                if (!xml.contains("name=\"$key\"")) missing += "$locale/$key"
            }
        }
        if (missing.isNotEmpty()) fail("missing string rows: ${missing.joinToString()}")
    }

    @Test
    fun `Cyrillic locales do not reuse the English wording`() {
        val english = stringsXml("values")
        listOf("values-uk", "values-ru").forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                val translated = valueOf(xml, key)
                assertTrue(
                    "$locale/$key is still the English text: $translated",
                    translated != valueOf(english, key),
                )
            }
        }
    }

    /**
     * The lapsed notice exists to say the schedules are still running and still
     * being paid for. Copy that drops either half turns it into a bare upsell.
     */
    @Test
    fun `the lapsed notice names both the price and the stop affordance`() {
        val body = valueOf(stringsXml("values"), "recurring_lapsed_notice_body").lowercase()

        assertTrue("no mention of the price the customer now pays: $body", body.contains("full"))
        assertTrue("no mention of pausing or deleting: $body", body.contains("pause"))
        assertTrue("no mention of pausing or deleting: $body", body.contains("delete"))
    }

    private fun valueOf(xml: String, key: String): String {
        val match = Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
        return match?.groupValues?.get(1) ?: fail("no <string name=\"$key\"> found").let { "" }
    }
}
