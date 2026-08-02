package cz.cleansia.partner.core

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Android falls back to `values/` for any row a locale is missing and this
 * module has no lint step in CI, so a half-finished translation compiles,
 * ships, and renders English to a cleaner who does not read it. The customer
 * app pins this in `NotificationsScreenTogglesTest`; the partner app had no
 * equivalent.
 */
class StringResourceParityTest {

    private val translations = listOf("values-cs", "values-sk", "values-uk", "values-ru")

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `all five locales declare the same string keys`() {
        assertSameKeys("string")
    }

    @Test
    fun `all five locales declare the same plurals`() {
        assertSameKeys("plurals")
    }

    private fun assertSameKeys(tag: String) {
        val english = keys("values", tag)
        assertTrue("values/strings.xml declares no <$tag>", english.isNotEmpty())
        translations.forEach { locale ->
            val translated = keys(locale, tag)
            assertEquals(
                "$locale is missing <$tag> rows: ${(english - translated).sorted()}; " +
                    "and declares rows values/ does not: ${(translated - english).sorted()}",
                english,
                translated,
            )
        }
    }

    private fun keys(locale: String, tag: String): Set<String> {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<$tag name=\"([^\"]+)\"")
            .findAll(file.readText())
            .map { it.groupValues[1] }
            .toSet()
    }
}
