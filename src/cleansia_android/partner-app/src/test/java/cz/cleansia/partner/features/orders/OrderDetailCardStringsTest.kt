package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The detail's scope line once concatenated its own copy (`"$rooms rooms"`), which no key-parity check could
 * see: text that never became a resource is invisible to `R`, to `lint`, and to every test that compares locale
 * key sets. The scope and crew lines are now the board's rules (`OrdersBoardRules`) drawn by the about panel
 * (`ScopeCard`), so this works on those source files and the resources directly.
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
    private val boardRules = card("OrdersBoardRules.kt")

    /** Every counted label the scope and crew lines render — property scope and crew alike. */
    private val scopePlurals = listOf("scope_rooms", "scope_baths", "crew_size", "orders_flag_crew_taken")

    @Test
    fun `the about panel carries no prose of its own`() {
        mapOf("ScopeCard.kt" to scopeCard)
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
    fun `every counted label on the scope and crew lines goes through a shared plural`() {
        scopePlurals.forEach { name ->
            assertTrue(
                "the scope or crew line must read R.plurals.$name, as the board does",
                scopeCard.contains("R.plurals.$name") || boardRules.contains("R.plurals.$name"),
            )
        }
        assertTrue("Property & scope names the package or service the board names", scopeCard.contains("rules.scopeLead("))
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
     * Owner ruling 2026-10-02: a bathroom is "ванна кімната" in Ukrainian and "ванная" in Russian. Bare
     * "ванна" or "ванні" read as bathtubs on the board, the offer, the contract and this card.
     */
    @Test
    fun `Ukrainian and Russian count bathrooms, not bathtubs`() {
        val expected = mapOf(
            "values-uk" to mapOf(
                "one" to "%1\$d ванна кімната",
                "few" to "%1\$d ванні кімнати",
                "many" to "%1\$d ванних кімнат",
                "other" to "%1\$d ванні кімнати",
            ),
            "values-ru" to mapOf(
                "one" to "%1\$d ванная",
                "few" to "%1\$d ванные",
                "many" to "%1\$d ванных",
                "other" to "%1\$d ванные",
            ),
        )
        expected.forEach { (locale, items) ->
            val declared = Regex("<item quantity=\"([^\"]+)\">(.*?)</item>")
                .findAll(pluralBody(stringsXml(locale), locale, "scope_baths"))
                .associate { it.groupValues[1] to it.groupValues[2] }
            assertEquals("$locale/scope_baths", items, declared)
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
