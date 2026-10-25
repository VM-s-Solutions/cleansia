package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Cancelling a job is the one place the cleaner learns what leaving does to the customer and to the job, and
 * iOS says it in the same keys and words. A translation that goes missing renders English; one that promises
 * more than the server does is worse.
 */
class DropOrderStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val keys = listOf(
        "order_drop_action",
        "order_drop_confirm_title",
        "order_drop_confirm_message",
        "order_drop_confirm_action",
        "order_drop_keep",
        "order_dropped_toast_no_crew",
        "order_dropped_toast",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every drop string is written and translated in all five locales, with no format slot`() {
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

    /** A dismiss labelled "Cancel" inside a "Cancel this job?" dialog reads as the confirm. */
    @Test
    fun `the dismiss button is not the generic cancel`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            assertNotEquals("$locale/order_drop_keep", valueOf(xml, "cancel"), valueOf(xml, "order_drop_keep"))
        }
    }

    /** A company may have nobody in the audience of the crew-lost alert, so the copy must not say anyone there is told. */
    @Test
    fun `no drop string says administrators or the company are told`() {
        val told = listOf("administrator", "the company", "administrátor", "společnost", "spoločnosť", "адміністратор", "компан", "администратор")
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                val value = valueOf(xml, key)!!.lowercase()
                told.forEach { word -> assertFalse("$locale/$key mentions \"$word\": $value", value.contains(word)) }
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
}
