package cz.cleansia.customer.features.membership

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [MembershipPerksTest] proves the resolver; it says nothing about whether the card still draws it, or
 * whether the labels it draws exist outside English. The pills shipped as four English literals for a
 * full release with the resolver green in every other regard, so the call site and the resource set are
 * pinned here too.
 */
class MembershipPerkPillBindingTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val pillKeys = listOf(
        "membership_perk_pill_discount",
        "membership_perk_pill_cancellation",
        "membership_perk_pill_recurring",
    )

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val card: String =
        File(moduleDir, "src/main/java/cz/cleansia/customer/features/membership/MembershipManagementCard.kt")
            .also { assertTrue("MembershipManagementCard.kt not found at ${it.absolutePath}", it.isFile) }
            .readText()

    @Test
    fun `the active card renders the resolved perks`() {
        assertTrue("the perk row lost its source", card.contains("MembershipPerks.resolve(response)"))
        assertTrue("the perks resolve but nothing draws them", card.contains("PerkPill(perk = perk"))
    }

    @Test
    fun `the card spells no perk label itself`() {
        listOf("Express", "Recurring", "% off", "h cancel").forEach { literal ->
            assertFalse(
                "\"$literal\" is a literal — cs/sk/uk/ru would read it in English",
                card.contains("\"$literal") || card.contains("$literal\""),
            )
        }
    }

    /** The express flag has no pricing behind it, so the card must not branch on it at all. */
    @Test
    fun `the card never reads the express flag`() {
        assertFalse(card.contains("allowsExpressUpgrade"))
    }

    @Test
    fun `every pill label is translated in every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            pillKeys.forEach { key ->
                val value = valueOf(xml, key)
                assertTrue("$locale/$key is missing", value != null)
                assertTrue("$locale/$key is blank", value!!.isNotBlank())
            }
        }
    }

    /**
     * A translator dropping `%1$d` turns the pill into a label that never names the number it exists to
     * communicate, and a bare `%` in a formatted string is a runtime `UnknownFormatConversionException`.
     */
    @Test
    fun `every translated pill keeps its placeholder and escapes its percent sign`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            listOf("membership_perk_pill_discount", "membership_perk_pill_cancellation").forEach { key ->
                val value = valueOf(xml, key)!!
                assertTrue("$locale/$key lost its %1\$d placeholder", value.contains("%1\$d"))
                assertEquals(
                    "$locale/$key has an unescaped % — String.format would throw",
                    0,
                    value.replace("%1\$d", "").replace("%%", "").count { it == '%' },
                )
            }
        }
    }

    private fun stringsXml(locale: String): String {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private fun valueOf(xml: String, key: String): String? =
        Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
            ?.groupValues
            ?.get(1)
}
