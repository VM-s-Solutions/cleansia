package cz.cleansia.partner.features.auth

import java.io.File
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/** The line that says why the confirm screen asks for a password renders English when a locale misses it. */
class ConfirmEmailPasswordStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    private fun valueOf(locale: String): String? =
        Regex("<string name=\"confirm_email_password_prompt\">(.*?)</string>")
            .find(File(resDir, "$locale/strings.xml").readText())
            ?.groupValues
            ?.get(1)

    @Test
    fun `the password prompt is written and translated in all five locales`() {
        locales.forEach { locale ->
            assertTrue("$locale/confirm_email_password_prompt is missing or blank", valueOf(locale)?.isNotBlank() == true)
            if (locale != "values") assertNotEquals("$locale is still English", valueOf("values"), valueOf(locale))
        }
    }
}
