package cz.cleansia.partner.features.profile

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

class LegalDocumentsStringsTest {

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    private val translations = listOf("values-cs", "values-sk", "values-uk", "values-ru")

    private val slots = mapOf(
        "legal_documents_title" to emptyList(),
        "legal_documents_intro" to emptyList(),
        "legal_documents_empty" to emptyList(),
        "legal_documents_version" to listOf("%1\$s"),
        "legal_documents_accepted_on" to listOf("%1\$s", "%2\$s"),
        "legal_documents_new_version" to listOf("%1\$s"),
        "legal_documents_awaiting" to emptyList(),
        "legal_documents_accept" to emptyList(),
        "legal_documents_accepted_toast" to emptyList(),
        "legal_documents_text_updated" to emptyList(),
        "legal_documents_review" to emptyList(),
        "profile_legal_documents_summary" to emptyList(),
        "registration_lock_category_legal_documents" to emptyList(),
        "registration_lock_action_accept_documents" to emptyList(),
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

    /** The documents bind the operating company of the cleaner's market, not the brand. */
    @Test
    fun `the intro names the operating company as the other party`() {
        (listOf("values") + translations).forEach { locale ->
            val intro = valueOf(stringsXml(locale), "legal_documents_intro")!!
            assertTrue("$locale/legal_documents_intro names Cleansia as the party: \"$intro\"", "Cleansia" !in intro)
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
