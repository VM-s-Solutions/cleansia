package cz.cleansia.partner.core

import cz.cleansia.partner.features.profile.HOW_JOBS_ARE_OFFERED_URL
import cz.cleansia.partner.features.profile.browserPackage
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * What a cleaner is told about the controls an administrator holds over their work: why they were
 * taken off a job, why their week is capped, and where the rules of the board are written down.
 */
class PlatformWorkCopyTest {

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    private val translations = listOf("values-cs", "values-sk", "values-uk", "values-ru")

    /** Each key with the format slots its sentence must carry, in every locale. */
    private val slots = mapOf(
        "order_removal_title" to emptyList(),
        "order_removal_message" to listOf("%1\$s"),
        "profile_weekly_limit" to listOf("%1\$d"),
        "profile_weekly_limit_reason" to listOf("%1\$s"),
        "profile_how_jobs_are_offered" to emptyList(),
        "profile_how_jobs_are_offered_summary" to emptyList(),
    )

    @Test
    fun `every locale carries the copy with the slots its sentence needs`() {
        (listOf("values") + translations).forEach { locale ->
            val xml = stringsXml(locale)
            slots.forEach { (key, expected) ->
                val value = valueOf(xml, key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
                assertEquals("$locale/$key", expected, formatSlots(value))
            }
        }
    }

    @Test
    fun `no translation is the English sentence copied over`() {
        val english = stringsXml("values")
        translations.forEach { locale ->
            val xml = stringsXml(locale)
            slots.keys.forEach { key ->
                assertNotEquals("$locale/$key is still English", valueOf(english, key), valueOf(xml, key))
            }
        }
    }

    @Test
    fun `no copy rates the partner on punctuality or pace`() {
        val scoring = Regex("(?i)\\bon[- ]time\\b|\\b(faster|slower)\\b|completion time")
        val offending = Regex("<string name=\"([^\"]+)\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .findAll(stringsXml("values"))
            .filter { scoring.containsMatchIn(it.groupValues[2]) }
            .map { it.groupValues[1] }
            .toList()

        assertEquals(emptyList<String>(), offending)
    }

    /**
     * A confirmed lockout pays the seat the reward its job's contract states, not a share of a fee, so the
     * line names it in the contract's own words.
     */
    @Test
    fun `the lockout line is the job's reward, not a share of a fee`() {
        (listOf("values") + translations).forEach { locale ->
            val xml = stringsXml(locale)
            val reward = valueOf(xml, "work_contract_reward")
            assertNotNull("$locale/strings.xml is missing work_contract_reward", reward)
            val line = valueOf(xml, "period_pay_line_lockout_fee_share")
            assertNotNull("$locale/strings.xml is missing period_pay_line_lockout_fee_share", line)
            assertTrue("$locale/period_pay_line_lockout_fee_share = $line", line!!.startsWith(reward!!))
        }
    }

    @Test
    fun `the rules page is the one the partner web serves`() {
        assertEquals("https://partner.cleansia.cz/how-jobs-are-offered", HOW_JOBS_ARE_OFFERED_URL)
    }

    @Test
    fun `the rules page opens in the default browser`() {
        assertEquals(CHROME, browserPackage(CHROME, listOf(FIREFOX, CHROME), OWN_PACKAGE))
    }

    @Test
    fun `with no default browser the rules page opens in an installed one, not the system chooser`() {
        assertEquals(FIREFOX, browserPackage("android", listOf(FIREFOX, CHROME), OWN_PACKAGE))
    }

    @Test
    fun `the rules page never opens in the partner app itself`() {
        assertEquals(CHROME, browserPackage(OWN_PACKAGE, listOf(OWN_PACKAGE, CHROME), OWN_PACKAGE))
        assertNull(browserPackage(OWN_PACKAGE, listOf(OWN_PACKAGE), OWN_PACKAGE))
    }

    @Test
    fun `the app can see the installed browsers on API 30 and later`() {
        val queries = Regex("<queries>(.*?)</queries>", RegexOption.DOT_MATCHES_ALL).find(manifest)?.groupValues?.get(1)
        assertNotNull("the manifest declares no <queries>, so API 30+ hides every browser", queries)
        assertTrue(queries!!.contains("android.intent.action.VIEW"))
        assertTrue(queries.contains("android.intent.category.BROWSABLE"))
        assertTrue(queries.contains("android:scheme=\"https\""))
    }

    private val manifest: String
        get() = File(resDir.parentFile, "AndroidManifest.xml").readText()

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

    private fun formatSlots(value: String): List<String> =
        Regex("%(\\d+\\\$)?[a-zA-Z]").findAll(value).map { it.value }.toList()

    private companion object {
        const val OWN_PACKAGE = "cz.cleansia.partner"
        const val CHROME = "com.android.chrome"
        const val FIREFOX = "org.mozilla.firefox"
    }
}
