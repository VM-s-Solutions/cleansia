package cz.cleansia.customer.features.profile

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The avatar surface is the one part of this feature no test on this module can
 * execute — there is no `androidTest` source set here — so a missing translation
 * would first be seen by a user reading English on a Czech phone. Cheap to pin,
 * and it is exactly the class of miss that ships.
 */
class ProfileAvatarStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val keys = listOf(
        "profile_avatar_change",
        "profile_avatar_remove",
        "profile_avatar_content_description",
        "profile_avatar_encode_failed",
        "profile_avatar_picker_unavailable_title",
        "profile_avatar_picker_unavailable_message",
        "profile_avatar_upload_success",
        "profile_avatar_remove_success",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every avatar string is translated in all five locales`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                val value = valueOf(xml, key)
                assertTrue("$locale/strings.xml is missing $key", value != null)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
            }
        }
    }

    @Test
    fun `the four translations are not the English string copied over`() {
        val english = keys.associateWith { valueOf(stringsXml("values"), it) }
        locales.drop(1).forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                assertTrue(
                    "$locale/strings.xml left $key in English",
                    valueOf(xml, key) != english[key],
                )
            }
        }
    }

    /**
     * "Updated" and "removed" are the two outcomes of the same tap, so a copy-paste
     * that leaves both reading the same sentence tells the user nothing at all — and
     * every locale is a fresh chance to make it.
     */
    @Test
    fun `the two save confirmations say different things in every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            assertTrue(
                "$locale/strings.xml uses one sentence for both avatar outcomes",
                valueOf(xml, "profile_avatar_upload_success") !=
                    valueOf(xml, "profile_avatar_remove_success"),
            )
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
