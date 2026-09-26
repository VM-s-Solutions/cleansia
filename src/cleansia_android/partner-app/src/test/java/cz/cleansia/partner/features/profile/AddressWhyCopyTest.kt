package cz.cleansia.partner.features.profile

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Cleaner pay has no distance component (owner ruling 2026-09-24), so no partner copy may promise pay
 * per kilometre — the address card used to give it as a reason for asking for a home address.
 */
class AddressWhyCopyTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /** Pay phrasing only — a bare "km" is the job radius and "cestovní pas" is a passport. */
    private val travelPayClaim = Regex(
        "travel pay|per kilomet|cestovné|za kilomet|за кілометр|за километр",
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
    fun `no partner string promises pay for the distance travelled`() {
        val claims = locales.flatMap { locale ->
            strings(locale)
                .filterValues { travelPayClaim.containsMatchIn(it) }
                .map { (key, value) -> "$locale/$key: $value" }
        }
        assertEquals(emptyList<String>(), claims)
    }

    @Test
    fun `the address card keeps the reasons that are still true in every locale`() {
        locales.forEach { locale ->
            val declared = strings(locale)
            listOf("address_why_reason_jobs", "address_why_reason_invoice").forEach { key ->
                assertTrue("$locale/$key is missing", declared[key]?.isNotBlank() == true)
            }
            assertTrue(
                "$locale still declares the travel-pay reason",
                "address_why_reason_distance_pay" !in declared,
            )
        }
    }
}
