package cz.cleansia.customer.features.auth

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The one sentence a customer gets when they tap Continue with Google without ticking the box.
 * Android falls back to `values/` for a missing row and this module has no lint step in CI, so a
 * dropped locale ships an English refusal on a screen the user is being blocked on.
 */
class SignUpSocialGateStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val key = "register_social_terms_required"

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
    fun `the social terms refusal exists in all five locales`() {
        val missing = locales.filterNot { stringsXml(it).contains("name=\"$key\"") }
        if (missing.isNotEmpty()) fail("missing $key in: ${missing.joinToString()}")
    }

    @Test
    fun `no locale reuses the English wording`() {
        val english = valueOf(stringsXml("values"), key)
        locales.filterNot { it == "values" }.forEach { locale ->
            val translated = valueOf(stringsXml(locale), key)
            assertTrue("$locale/$key is still the English text: $translated", translated != english)
        }
    }

    /**
     * The refusal has to name the two documents the tick covers and the provider it is blocking,
     * or it reads as an unexplained failure on a screen whose checkbox may be scrolled off.
     */
    @Test
    fun `the refusal names both documents and the provider`() {
        val message = valueOf(stringsXml("values"), key).lowercase()

        assertTrue("no mention of the terms: $message", message.contains("terms"))
        assertTrue("no mention of the privacy policy: $message", message.contains("privacy"))
        assertTrue("no mention of the provider being blocked: $message", message.contains("google"))
    }

    private fun valueOf(xml: String, name: String): String {
        val match = Regex("<string name=\"$name\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
        return match?.groupValues?.get(1) ?: fail("no <string name=\"$name\"> found").let { "" }
    }
}
