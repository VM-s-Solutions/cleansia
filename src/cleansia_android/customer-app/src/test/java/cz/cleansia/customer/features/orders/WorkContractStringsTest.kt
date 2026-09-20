package cz.cleansia.customer.features.orders

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The copy of the customer's contract surfaces: the wizard sentence, the detail line, and the
 * contract screen. A key with no resource in a locale falls back to English without breaking the
 * build, and the roster is the only thing that reads all five files. The sentence's markup is
 * pinned by `ConsentCatalogTest` in `:core`, beside the consent sentences it is the twin of.
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
        "booking_work_contract_notice",
        "work_contract_title",
        "work_contract_accepted_line",
        "work_contract_read",
        "work_contract_version",
        "work_contract_facts_title",
        "work_contract_order_number",
        "work_contract_window",
        "work_contract_price",
        "work_contract_location",
        "work_contract_accepted_on",
        "work_contract_accepted_in_language",
        "work_contract_load_error",
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

    @Test
    fun `every placeholder survives translation`() {
        val expected = mapOf(
            "work_contract_accepted_line" to listOf("%1\$s", "%2\$s", "%3\$s"),
            "work_contract_version" to listOf("%1\$s"),
            "work_contract_accepted_on" to listOf("%1\$s", "%2\$s"),
            "work_contract_accepted_in_language" to listOf("%1\$s"),
        )
        locales.forEach { locale ->
            val declared = strings(locale)
            expected.forEach { (key, placeholders) ->
                val value = declared.getValue(key)
                placeholders.forEach { placeholder ->
                    assertTrue("$locale/$key no longer carries $placeholder: \"$value\"", placeholder in value)
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

    /** The wizard sentence is an information line — a link, never a tick — and it names no figure. */
    @Test
    fun `the wizard sentence links the public page and carries no digit`() {
        locales.forEach { locale ->
            val value = strings(locale).getValue("booking_work_contract_notice")
            assertTrue("$locale/booking_work_contract_notice does not link the page: $value", "cleansia://work-contract" in value)
            assertFalse("$locale/booking_work_contract_notice carries a figure: $value", value.any { it.isDigit() })
        }
    }
}
