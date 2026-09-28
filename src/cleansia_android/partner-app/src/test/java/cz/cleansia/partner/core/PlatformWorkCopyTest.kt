package cz.cleansia.partner.core

import cz.cleansia.partner.features.profile.HOW_JOBS_ARE_OFFERED_URL
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * What a cleaner is told about the controls an administrator holds over their work: why they were
 * taken off a job, why their week is capped, and where the rules of the board are written down.
 */
class PlatformWorkCopyTest {

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    private val translations = listOf("values-cs", "values-sk", "values-uk", "values-ru")

    /** Each key with the format slots its sentence must carry, in every locale. */
    private val slots = mapOf(
        "order_removal_title" to emptyList(),
        "order_removal_message" to listOf("%1\$s"),
        "profile_weekly_limit" to listOf("%1\$d"),
        "profile_weekly_limit_reason" to listOf("%1\$s"),
        "profile_how_jobs_are_offered" to emptyList(),
        "profile_how_jobs_are_offered_summary" to emptyList(),
    )

    @Test
    fun `every locale carries the copy with the slots its sentence needs`() {
        (listOf("values") + translations).forEach { locale ->
            val xml = stringsXml(locale)
            slots.forEach { (key, expected) ->
                val value = valueOf(xml, key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
                assertEquals("$locale/$key", expected, formatSlots(value))
            }
        }
    }

    @Test
    fun `no translation is the English sentence copied over`() {
        val english = stringsXml("values")
        translations.forEach { locale ->
            val xml = stringsXml(locale)
            slots.keys.forEach { key ->
                assertNotEquals("$locale/$key is still English", valueOf(english, key), valueOf(xml, key))
            }
        }
    }

    @Test
    fun `the rules page is the one the partner web serves`() {
        assertEquals("https://partner.cleansia.cz/how-jobs-are-offered", HOW_JOBS_ARE_OFFERED_URL)
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
