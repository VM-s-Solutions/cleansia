package cz.cleansia.customer.features.profile

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * `profile_row_edit` (the width-capped hero chip) and `profile_edit_title` (the
 * screen header) held identical wording until the chip label was cut back to the
 * bare verb. They read as duplicates and the obvious tidy-up is to merge them —
 * which silently restores the overflow the cut was made to fix, on a surface no
 * unit test otherwise touches.
 *
 * Pinning the chip label as a strict prefix of the title also pins how the four
 * non-English forms were produced: each is the leading verb of the long string a
 * translator already approved, not a fresh translation.
 */
class ProfileEditLabelStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `the hero chip label is the title's leading verb in every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            val chip = valueOf(xml, locale, "profile_row_edit")
            val title = valueOf(xml, locale, "profile_edit_title")
            assertTrue(
                "$locale: chip label \"$chip\" must be the leading verb of \"$title\"",
                title.startsWith(chip) && chip.length < title.length,
            )
        }
    }

    private fun stringsXml(locale: String): String {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private fun valueOf(xml: String, locale: String, key: String): String =
        Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
            ?.groupValues
            ?.get(1)
            ?: fail("no <string name=\"$key\"> in $locale/strings.xml").let { "" }
}
