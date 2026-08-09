package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * Two cards on the order detail printed English into every locale, and neither
 * defect was visible to a key-parity check: the scope line concatenated its own
 * copy (`"$rooms rooms"`) and the timeline printed the backend's enum name
 * (`entry.status.name`). Text that never became a resource is invisible to `R`,
 * to `lint`, and to every test that compares locale key sets.
 *
 * Both cards are single `@Composable`s with no seam, so this works on the
 * source and resource files directly — the `NotificationsScreenTogglesTest`
 * precedent. `OrderStatusPresentationTest` pins the resolver these call; this
 * pins that the cards still call it, which a resolver test cannot see.
 */
class OrderDetailCardStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /**
     * CLDR plural categories each language actually needs. Android silently
     * falls back to `other` for a missing category, so "2 pokojů" instead of
     * "2 pokoje" ships without a warning.
     */
    private val requiredQuantities = mapOf(
        "values" to setOf("one", "other"),
        "values-cs" to setOf("one", "few", "other"),
        "values-sk" to setOf("one", "few", "other"),
        "values-uk" to setOf("one", "few", "many", "other"),
        "values-ru" to setOf("one", "few", "many", "other"),
    )

    private val moduleDir: File = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).firstOrNull { File(it, "src/main/res/values/strings.xml").isFile }
        ?: error("partner-app module not found from working dir ${File(".").absolutePath}")

    private val scopeCard = card("ScopeCard.kt")
    private val statusTimeline = card("StatusTimeline.kt")

    /** Every counted label the scope card renders — property scope and crew alike. */
    private val scopePlurals = listOf("scope_rooms", "scope_baths", "crew_size", "crew_spots_open")

    @Test
    fun `neither card carries prose of its own`() {
        mapOf("ScopeCard.kt" to scopeCard, "StatusTimeline.kt" to statusTimeline)
            .forEach { (name, source) ->
                val prose = userFacingLiterals(source)
                if (prose.isNotEmpty()) {
                    fail(
                        "$name renders text that is not a string resource, so cs/sk/uk/ru " +
                            "cleaners read English: ${prose.joinToString { "\"$it\"" }}",
                    )
                }
            }
    }

    @Test
    fun `every counted label on the scope card goes through a shared plural`() {
        scopePlurals.forEach { name ->
            assertTrue(
                "ScopeCard must read R.plurals.$name — the orders list already does",
                scopeCard.contains("R.plurals.$name"),
            )
        }
    }

    @Test
    fun `every scope plural declares every quantity its language needs`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            scopePlurals.forEach { name ->
                val quantities = Regex("quantity=\"([^\"]+)\"")
                    .findAll(pluralBody(xml, locale, name))
                    .map { it.groupValues[1] }
                    .toSet()
                assertEquals("$locale/$name", requiredQuantities.getValue(locale), quantities)
            }
        }
    }

    /**
     * The wire `name` ("OnTheWay") is only ever a debug diagnostic, and the one
     * place that decides so is [orderStatusLabel]. Reading it at the call site
     * puts it back on screen in every locale.
     */
    @Test
    fun `the timeline resolves its label through the presentation seam`() {
        assertTrue(
            "StatusTimeline must label rows with orderStatusLabel(…)",
            statusTimeline.contains("orderStatusLabel("),
        )
        val rawReads = statusTimeline.lines()
            .filter { it.contains("status?.name") || it.contains("status.name") }
            .filterNot { it.contains("orderStatusLabel(") }
        if (rawReads.isNotEmpty()) {
            fail(
                "StatusTimeline reads the backend's English enum name outside " +
                    "orderStatusLabel: ${rawReads.joinToString(" | ") { it.trim() }}",
            )
        }
    }

    private fun card(name: String): String =
        File(moduleDir, "src/main/java/cz/cleansia/partner/features/orders/$name")
            .also { assertTrue("$name not found at ${it.absolutePath}", it.isFile) }
            .readText()

    private fun stringsXml(locale: String): String {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private fun pluralBody(xml: String, locale: String, name: String): String =
        Regex("<plurals name=\"$name\">(.*?)</plurals>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
            ?.groupValues
            ?.get(1)
            ?: fail("no <plurals name=\"$name\"> in $locale/strings.xml").let { "" }

    /**
     * String literals left after comments and `${…}` templates are stripped,
     * keeping only those with a run of three or more letters — which separators
     * ("·"), placeholders ("#") and the em dash are not, and prose always is.
     */
    private fun userFacingLiterals(source: String): List<String> {
        var stripped = source
            .replace(Regex("/\\*.*?\\*/", RegexOption.DOT_MATCHES_ALL), "")
            .replace(Regex("//[^\n]*"), "")
        var previous: String
        do {
            previous = stripped
            stripped = stripped.replace(Regex("\\$\\{[^{}]*}"), "")
        } while (stripped != previous)
        stripped = stripped.replace(Regex("\\$[A-Za-z_][A-Za-z0-9_]*"), "")

        return Regex("\"((?:\\\\.|[^\"\\\\\n])*)\"")
            .findAll(stripped)
            .map { it.groupValues[1] }
            .filter { it.contains(Regex("[A-Za-z]{3,}")) }
            .toList()
    }
}
