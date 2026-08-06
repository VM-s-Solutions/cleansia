package cz.cleansia.customer.features.recurring

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * Saving an edit rewrites a schedule that is already spawning real, priced
 * bookings, and the notice on the last wizard step is the only place the
 * customer is told what that does and does not touch. Android silently falls
 * back to `values/` for a missing locale row and this module has no lint step
 * in CI, so a dropped row ships and renders English on the one screen that
 * explains a money-adjacent action.
 *
 * Presence is asserted per locale rather than by comparing the locales to each
 * other: a key dropped from all five is still a defect, and a locale-to-locale
 * comparison cannot see it.
 */
class RecurringEditNoticeStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val noticeKeys = listOf(
        "recurring_edit_notice_title",
        "recurring_edit_notice_what_changes",
        "recurring_edit_notice_what_stays",
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
    fun `edit-impact notice is present in all five locales`() {
        val missing = mutableListOf<String>()
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            noticeKeys.forEach { key ->
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
            noticeKeys.forEach { key ->
                val en = valueOf(english, key)
                val translated = valueOf(xml, key)
                assertTrue(
                    "$locale/$key is still the English text: $translated",
                    translated != en,
                )
            }
        }
    }

    private fun valueOf(xml: String, key: String): String {
        val match = Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
        return match?.groupValues?.get(1) ?: fail("no <string name=\"$key\"> found").let { "" }
    }
}
