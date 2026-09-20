package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The four keys the contract surface can receive and the copy the sheet and the detail render. A
 * key with no resource in a locale falls back to English or, for the error keys, to the raw
 * `contract.text_mismatch` on the snackbar — neither breaks the build, and the roster is the only
 * thing that reads all five files.
 */
class WorkContractStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    private fun strings(locale: String): Map<String, String> =
        Regex("<string name=\"([^\"]+)\">(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .findAll(File(resDir, "$locale/strings.xml").readText())
            .associate { it.groupValues[1] to it.groupValues[2] }

    /** `BusinessErrorMessage.Contract*` and the one `Legal` key the preview can answer. */
    private val errorKeys = listOf(
        "contract.not_accepted",
        "contract.text_mismatch",
        "contract.acceptance_required",
        "legal.document_not_found",
    )

    private val copy = listOf(
        "work_contract_title",
        "work_contract_version",
        "work_contract_order_number",
        "work_contract_swipe_to_accept",
        "work_contract_accepting",
        "work_contract_load_error",
        "work_contract_accepted_on",
        "work_contract_accepted_in_language",
        "work_contract_accepted_line",
        "work_contract_pending_banner",
        "work_contract_accept_cta",
        "work_contract_read",
    )

    @Test
    fun `every contract error key resolves to a sentence in all five locales`() {
        locales.forEach { locale ->
            val declared = strings(locale)
            errorKeys.forEach { key ->
                val normalized = key.replace('.', '_').replace('-', '_').lowercase()
                val name = "error_$normalized"
                assertTrue("$locale/strings.xml is missing $name", name in declared)
                assertTrue("$locale/strings.xml leaves $name empty", declared.getValue(name).isNotBlank())
            }
        }
    }

    @Test
    fun `every sheet and detail string is written in all five locales`() {
        locales.forEach { locale ->
            val declared = strings(locale)
            copy.forEach { key ->
                assertTrue("$locale/strings.xml is missing $key", key in declared)
                assertTrue("$locale/strings.xml leaves $key empty", declared.getValue(key).isNotBlank())
            }
        }
    }

    @Test
    fun `every placeholder survives translation`() {
        val expected = mapOf(
            "work_contract_version" to listOf("%1\$s"),
            "work_contract_order_number" to listOf("%1\$s"),
            "work_contract_accepted_on" to listOf("%1\$s", "%2\$s"),
            "work_contract_accepted_in_language" to listOf("%1\$s"),
            "work_contract_accepted_line" to listOf("%1\$s", "%2\$s"),
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
            (copy + errorKeys.map { "error_" + it.replace('.', '_') }).forEach { key ->
                val value = declared.getValue(key)
                assertTrue("$locale/$key carries an unescaped apostrophe: \"$value\"", !Regex("(?<!\\\\)'").containsMatchIn(value))
            }
        }
    }
}
