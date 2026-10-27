package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The crew close is new to every cleaner on a crew job, and the footer is the only place it explains
 * itself. A translation that goes missing renders English.
 */
class CrewCloseStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val keys = listOf(
        "slide_to_mark_my_part_done",
        "order_complete_job",
        "marking_my_part_done",
        "order_my_part_done_waiting",
        "order_my_part_done_toast",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every crew close string is written and translated in all five locales, with no format slot`() {
        val english = stringsXml("values")
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                val value = valueOf(xml, key)
                assertTrue("$locale/$key is missing or blank", value?.isNotBlank() == true)
                if (locale != "values") assertNotEquals("$locale/$key is still English", valueOf(english, key), value)
                assertFalse("$locale/$key takes no argument: $value", value!!.contains('%'))
            }
        }
    }

    private fun stringsXml(locale: String) = File(resDir, "$locale/strings.xml").readText()

    private fun valueOf(xml: String, key: String): String? =
        Regex("<string name=\"$key\">(.*?)</string>", RegexOption.DOT_MATCHES_ALL).find(xml)?.groupValues?.get(1)
}
