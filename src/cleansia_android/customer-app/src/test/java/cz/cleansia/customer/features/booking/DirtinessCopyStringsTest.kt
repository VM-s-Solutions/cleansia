package cz.cleansia.customer.features.booking

import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.DirtinessLevel
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The level descriptions are what the customer chooses by and what a disputed clean is measured
 * against, so every locale carries all of them, and the rate each one states is BookingPolicy's.
 */
class DirtinessCopyStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val levels = listOf("normal", "increased", "heavy")

    private val keys = listOf("dirtiness_title", "dirtiness_intro", "dirtiness_hint", "dirtiness_level_label") +
        levels.flatMap { level ->
            listOf("title", "price", "when", "detail_1", "detail_2", "detail_3", "detail_4")
                .map { "dirtiness_${level}_$it" }
        } +
        listOf("dirtiness_surcharge_increased", "dirtiness_surcharge_heavy")

    private val statedRates = mapOf(
        "dirtiness_normal_price" to emptyList(),
        "dirtiness_increased_price" to listOf(30),
        "dirtiness_heavy_price" to listOf(60),
        "dirtiness_surcharge_increased" to listOf(30),
        "dirtiness_surcharge_heavy" to listOf(60),
    )

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every dirtiness string is written in all five locales`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                assertTrue("$locale/$key is missing or blank", valueOf(xml, key)?.isNotBlank() == true)
            }
        }
    }

    @Test
    fun `the Cyrillic locales are not the English copy`() {
        val english = stringsXml("values")
        val words = keys - setOf("dirtiness_increased_price", "dirtiness_heavy_price")
        listOf("values-uk", "values-ru").forEach { locale ->
            val xml = stringsXml(locale)
            words.forEach { key ->
                assertTrue("$locale/$key is still English", valueOf(xml, key) != valueOf(english, key))
            }
        }
    }

    @Test
    fun `each level states the rate BookingPolicy charges for it`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            statedRates.forEach { (key, rates) ->
                val stated = Regex("""(\d+)\s*%""").findAll(valueOf(xml, key)!!).map { it.groupValues[1].toInt() }
                assertEquals("$locale/$key", rates, stated.toList())
            }
        }
    }

    @Test
    fun `every level has a title and only the surcharged ones have a summary line`() {
        assertEquals(R.string.dirtiness_normal_title, DirtinessLevel.Normal.titleRes())
        assertEquals(R.string.dirtiness_increased_title, DirtinessLevel.Increased.titleRes())
        assertEquals(R.string.dirtiness_heavy_title, DirtinessLevel.Heavy.titleRes())

        assertNull(DirtinessLevel.Normal.surchargeLineRes())
        assertEquals(R.string.dirtiness_surcharge_increased, DirtinessLevel.Increased.surchargeLineRes())
        assertEquals(R.string.dirtiness_surcharge_heavy, DirtinessLevel.Heavy.surchargeLineRes())
    }

    @Test
    fun `the booking cannot pass the level step without a chosen level`() {
        val sheet = source("features/booking/BookingBottomSheet.kt")
        assertTrue("the sheet no longer shows the level step", sheet.contains("2 -> DirtinessStep("))
        assertTrue("the level step no longer gates Continue", sheet.contains("2 -> state.dirtinessLevel != null"))
    }

    @Test
    fun `the recurring wizard asks for the level with the same descriptions`() {
        assertTrue(
            "the recurring wizard no longer renders the level picker",
            source("features/recurring/CreateRecurringScreen.kt").contains("DirtinessLevelPicker("),
        )
    }

    private fun source(relative: String): String =
        File(moduleDir, "src/main/java/cz/cleansia/customer/$relative")
            .also { assertTrue("$relative not found", it.isFile) }
            .readText()

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
