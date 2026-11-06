package cz.cleansia.customer.features.booking

import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.DirtinessLevel
import cz.cleansia.customer.features.orders.bookedSurchargeLineRes
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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

    private val bookedLines = listOf("dirtiness_surcharge_line_increased", "dirtiness_surcharge_line_heavy")

    private val keys = listOf("dirtiness_title", "dirtiness_torn_hint", "dirtiness_level_label") +
        levels.flatMap { level ->
            listOf("title", "price", "when", "detail_1", "detail_2", "detail_3", "detail_4")
                .map { "dirtiness_${level}_$it" }
        } +
        listOf("dirtiness_surcharge_increased", "dirtiness_surcharge_heavy") +
        bookedLines

    private val statedRates = mapOf(
        "dirtiness_normal_price" to emptyList(),
        "dirtiness_increased_price" to listOf(15),
        "dirtiness_heavy_price" to listOf(30),
        "dirtiness_surcharge_increased" to listOf(15),
        "dirtiness_surcharge_heavy" to listOf(30),
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
    fun `a booked order names no rate beside its surcharge, it may have been booked at another`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            bookedLines.forEach { key ->
                val value = valueOf(xml, key)!!
                assertTrue("$locale/$key states a rate: $value", value.none { it == '%' || it.isDigit() })
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

        assertNull(DirtinessLevel.Normal.bookedSurchargeLineRes())
        assertEquals(R.string.dirtiness_surcharge_line_increased, DirtinessLevel.Increased.bookedSurchargeLineRes())
        assertEquals(R.string.dirtiness_surcharge_line_heavy, DirtinessLevel.Heavy.bookedSurchargeLineRes())
    }

    @Test
    fun `the booking labels its surcharge with the rate it charges and the order detail with none`() {
        assertTrue(
            "the confirm step no longer states the rate it charges",
            source("features/booking/ConfirmStep.kt").contains("dirtinessLevel?.surchargeLineRes()"),
        )
        assertTrue(
            "the order detail no longer uses its rate-free line",
            source("features/orders/OrderDetailSummary.kt").contains("dirtinessLevel.bookedSurchargeLineRes()"),
        )
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

    @Test
    fun `only the chosen level lists its signs`() {
        DirtinessLevel.entries.forEach { level ->
            assertTrue("$level lists signs with nothing chosen", level.signsShownRes(selected = null).isEmpty())
        }
        DirtinessLevel.entries.forEach { chosen ->
            DirtinessLevel.entries.forEach { level ->
                val signs = level.signsShownRes(selected = chosen)
                if (level == chosen) {
                    assertEquals("$level chosen", 4, signs.size)
                } else {
                    assertTrue("$level lists signs while $chosen is chosen", signs.isEmpty())
                }
            }
        }
    }

    @Test
    fun `changing the choice moves the signs to the new level`() {
        assertEquals(4, DirtinessLevel.Normal.signsShownRes(selected = DirtinessLevel.Normal).size)
        assertTrue(DirtinessLevel.Normal.signsShownRes(selected = DirtinessLevel.Heavy).isEmpty())
        assertEquals(4, DirtinessLevel.Heavy.signsShownRes(selected = DirtinessLevel.Heavy).size)
        assertTrue(
            DirtinessLevel.Heavy.signsShownRes(selected = DirtinessLevel.Heavy)
                .none { it in DirtinessLevel.Normal.signsShownRes(selected = DirtinessLevel.Normal) },
        )
    }

    @Test
    fun `each level card is one radio element reading its title, surcharge, when and only its own shown signs`() {
        val card = source("features/booking/DirtinessLevelPicker.kt").substringAfter("private fun LevelCard(")
        assertTrue("the card is no longer one selectable radio", card.contains(".selectable(selected = selected, role = Role.RadioButton"))
        listOf("level.titleRes()", "level.priceRes()", "level.whenRes()", "level.signsShownRes(").forEach {
            assertTrue("the card no longer reads $it", card.contains(it))
        }
        assertFalse("the card lists every sign whether chosen or not", card.contains("level.detailRes()"))
    }

    @Test
    fun `the step opens on the levels and closes on the hint`() {
        val picker = source("features/booking/DirtinessLevelPicker.kt")
            .substringAfter("internal fun DirtinessLevelPicker(")
            .substringBefore("private fun LevelCard(")
        assertFalse("the intro line is back", picker.contains("dirtiness_intro"))
        val hint = picker.indexOf("R.string.dirtiness_torn_hint")
        assertTrue("the hint is missing", hint >= 0)
        assertTrue("the hint comes before the levels", picker.indexOf("LevelCard(") in 0 until hint)
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
