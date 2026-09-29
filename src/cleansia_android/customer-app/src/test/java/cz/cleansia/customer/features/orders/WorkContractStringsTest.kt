package cz.cleansia.customer.features.orders

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The copy of the customer's contract at the review step: the sentence naming who the contract is
 * with, and the request to start within the withdrawal period. A key with no resource in a locale
 * falls back to English without breaking the build, and the roster is the only thing that reads all
 * five files. The sentence's markup is pinned by `ConsentCatalogTest` in `:core`.
 */
class WorkContractStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    private fun strings(locale: String): Map<String, String> =
        Regex("<string name=\"([^\"]+)\">(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .findAll(File(resDir, "$locale/strings.xml").readText())
            .associate { it.groupValues[1] to it.groupValues[2] }

    private val copy = listOf(
        "booking_contract_notice",
        "consent_early_performance_draft_2026_09_29",
    )

    @Test
    fun `every contract string is written in all five locales`() {
        val english = strings("values")
        locales.forEach { locale ->
            val declared = strings(locale)
            copy.forEach { key ->
                assertTrue("$locale/strings.xml is missing $key", key in declared)
                assertTrue("$locale/strings.xml leaves $key empty", declared.getValue(key).isNotBlank())
                if (locale != "values") {
                    assertFalse("$locale/strings.xml carries the English $key", declared[key] == english[key])
                }
            }
        }
    }

    /** An unescaped apostrophe fails `mergeDebugResources` with an error that names no file or line. */
    @Test
    fun `no contract string carries a bare apostrophe`() {
        locales.forEach { locale ->
            val declared = strings(locale)
            copy.forEach { key ->
                val value = declared.getValue(key)
                assertFalse(
                    "$locale/$key carries an unescaped apostrophe: \"$value\"",
                    Regex("(?<!\\\\)'").containsMatchIn(value),
                )
            }
        }
    }

    /** The customer contracts with the operating company under the terms; the contract for work is the cleaner's. */
    @Test
    fun `the review sentence links the terms and no contract for work`() {
        locales.forEach { locale ->
            val value = strings(locale).getValue("booking_contract_notice")
            assertTrue("$locale/booking_contract_notice does not link the terms: $value", "cleansia://terms" in value)
            assertFalse("$locale/booking_contract_notice links the contract for work: $value", "cleansia://work-contract" in value)
            assertFalse("$locale/booking_contract_notice carries a figure: $value", value.any { it.isDigit() })
        }
    }

    @Test
    fun `the early-performance request names the statutory 14-day period`() {
        locales.forEach { locale ->
            val value = strings(locale).getValue("consent_early_performance_draft_2026_09_29")
            assertTrue("$locale/consent_early_performance_draft_2026_09_29 names no 14-day period: $value", "14" in value)
        }
    }
}
