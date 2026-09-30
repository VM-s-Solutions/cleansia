package cz.cleansia.partner.features.profile

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * No self-billing agreement exists in the app yet (ADR-0041 was never built), so the offboarding
 * screen may not list one among the records kept — only the invoices and pay records that do exist.
 */
class DeleteAccountKeptCopyTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val agreementClaim = Regex(
        "self-billing|samofaktur|самовистав|самовыстав",
        RegexOption.IGNORE_CASE,
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    private fun strings(locale: String): Map<String, String> {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<string name=\"([^\"]+)\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .findAll(file.readText())
            .associate { it.groupValues[1] to it.groupValues[2] }
    }

    @Test
    fun `no deletion string promises a self-billing agreement`() {
        val claims = locales.flatMap { locale ->
            strings(locale)
                .filterKeys { it.startsWith("delete_account_") }
                .filterValues { agreementClaim.containsMatchIn(it) }
                .map { (key, value) -> "$locale/$key: $value" }
        }
        assertEquals(emptyList<String>(), claims)
    }

    @Test
    fun `the kept list names the invoices and pay records in every locale`() {
        locales.forEach { locale ->
            val declared = strings(locale)
            listOf("delete_account_kept_invoices", "delete_account_kept_pay").forEach { key ->
                assertTrue("$locale/$key is missing", declared[key]?.isNotBlank() == true)
            }
        }
        val screen = File(resDir.parentFile, "java/cz/cleansia/partner/features/profile/DeleteAccountScreen.kt")
        assertTrue("DeleteAccountScreen.kt not found next to res/", screen.isFile)
        assertTrue(
            "the screen still renders an agreement row",
            !screen.readText().contains("delete_account_kept_agreement"),
        )
    }
}
