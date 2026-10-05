package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The non-payment confirm is the one place the cleaner learns what the tap does to the customer, and the
 * amount is the server's figure. A translation that drops or adds a slot renders the wrong sentence or
 * crashes `getString` on the money screen.
 */
class CashNotPaidStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val keys = listOf(
        "order_cash_not_paid_action",
        "order_cash_not_paid_confirm_title",
        "order_cash_not_paid_confirm_message",
        "order_cash_not_paid_confirm_message_no_amount",
        "order_cash_not_paid_confirm_action",
        "order_cash_not_paid_reported_toast",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every non-payment string is written and translated in all five locales`() {
        val english = stringsXml("values")
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                val value = valueOf(xml, key)
                assertTrue("$locale/$key is missing or blank", value?.isNotBlank() == true)
                if (locale != "values") assertTrue("$locale/$key is still English", value != valueOf(english, key))
            }
        }
    }

    @Test
    fun `only the amount message takes the amount, and it takes it once`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                val expected = if (key == "order_cash_not_paid_confirm_message") listOf("%1\$s") else emptyList()
                assertEquals("$locale/$key format slots", expected, formatSlots(valueOf(xml, key)!!))
            }
        }
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

    private fun formatSlots(value: String): List<String> =
        Regex("%(\\d+\\\$)?[a-zA-Z]").findAll(value).map { it.value }.toList()
}
