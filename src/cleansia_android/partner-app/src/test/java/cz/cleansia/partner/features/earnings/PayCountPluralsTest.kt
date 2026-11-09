package cz.cleansia.partner.features.earnings

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The six counts on the pay screens printed "1 jobs" while they were plain strings. A plural only helps if it carries
 * every form its language picks for a whole number, and every form prints the number the call site passes.
 */
class PayCountPluralsTest {

    private val keys = listOf(
        "earnings_jobs_done_count",
        "earnings_days_remaining",
        "invoices_summary_count",
        "invoice_card_jobs_count",
        "invoice_period_jobs",
        "period_pay_jobs_count",
    )

    private val formsByLocale = mapOf(
        "values" to setOf("one", "other"),
        "values-cs" to setOf("one", "few", "other"),
        "values-sk" to setOf("one", "few", "other"),
        "values-uk" to setOf("one", "few", "many", "other"),
        "values-ru" to setOf("one", "few", "many", "other"),
    )

    @Test
    fun `each pay count is a plural with its language's forms, and every form prints the number`() {
        formsByLocale.forEach { (locale, forms) ->
            val xml = strings(locale)
            keys.forEach { key ->
                assertFalse("$locale still declares <string name=\"$key\">", xml.contains("<string name=\"$key\""))
                val items = items(xml, locale, key)
                assertEquals("$locale <plurals name=\"$key\"> forms", forms, items.keys)
                items.forEach { (quantity, text) ->
                    assertTrue("$locale $key/$quantity drops the number: $text", Regex("""%(1\$)?d""").containsMatchIn(text))
                }
            }
        }
    }

    @Test
    fun `English names one job differently from two`() {
        val xml = strings("values")
        keys.forEach { key ->
            val items = items(xml, "values", key)
            assertNotEquals(key, items["one"], items["other"])
        }
    }

    private fun items(xml: String, locale: String, key: String): Map<String, String> {
        val block = Regex("""<plurals name="$key">(.*?)</plurals>""", RegexOption.DOT_MATCHES_ALL).find(xml)
        assertTrue("$locale declares no <plurals name=\"$key\">", block != null)
        return Regex("""<item quantity="(\w+)">(.*?)</item>""")
            .findAll(block!!.groupValues[1])
            .associate { it.groupValues[1] to it.groupValues[2] }
    }

    private fun strings(locale: String): String {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")
}
