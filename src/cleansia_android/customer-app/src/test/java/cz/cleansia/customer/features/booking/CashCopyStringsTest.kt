package cz.cleansia.customer.features.booking

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Why cash is not offered is the whole feature on the payment step, and a missing locale row falls back
 * to English on a screen about money. The crew count is the server's, so it must stay a placeholder.
 */
class CashCopyStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val keys = listOf(
        "booking_cash_needs_account",
        "booking_cash_needs_card",
        "booking_cash_pending",
        "booking_cash_cleared",
        "recurring_cash_needs_card",
        "recurring_cash_pending",
        "recurring_cash_cleared",
        "recurring_cash_unchecked",
        "recurring_create_payment_missing",
        "recurring_status_needs_change",
        "recurring_cash_change_title",
        "recurring_cash_change_body",
        "recurring_cash_change_action",
    )

    private val crewCounts = listOf("booking_cash_needs_card", "recurring_cash_needs_card")

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every cash string is written in all five locales`() {
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
        listOf("values-uk", "values-ru").forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { key ->
                assertTrue("$locale/$key is still English", valueOf(xml, key) != valueOf(english, key))
            }
        }
    }

    @Test
    fun `the crew count is the server's figure, never one of the copy's own`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            crewCounts.forEach { key ->
                val value = valueOf(xml, key)!!
                assertTrue("$locale/$key lost its %1\$d placeholder", value.contains("%1\$d"))
                assertEquals(
                    "$locale/$key names a number of its own",
                    emptyList<Char>(),
                    value.replace("%1\$d", "").filter { it.isDigit() }.toList(),
                )
            }
        }
    }

    @Test
    fun `the payment step says why cash is not offered, for every verdict`() {
        val confirm = source("features/booking/ConfirmStep.kt")
        listOf("booking_cash_needs_account", "booking_cash_needs_card", "booking_cash_pending", "booking_cash_cleared")
            .forEach { key -> assertTrue("ConfirmStep no longer renders $key", confirm.contains("R.string.$key")) }
        assertTrue(
            "the cash option is no longer gated on the verdict",
            confirm.contains("enabled = cashEligibility == CashEligibility.Available"),
        )

        val recurring = source("features/recurring/CreateRecurringScreen.kt")
        listOf(
            "recurring_cash_needs_card",
            "recurring_cash_pending",
            "recurring_cash_cleared",
            "recurring_create_payment_missing",
        ).forEach { key -> assertTrue("the recurring wizard no longer renders $key", recurring.contains("R.string.$key")) }
        assertTrue(
            "the recurring cash card is no longer gated on the verdict",
            recurring.contains("cashEnabled = cashEligibility == CashEligibility.Available"),
        )
    }

    @Test
    fun `the sheet tells the booking whether it is on screen, so a cleared cash choice is announced only there`() {
        assertTrue(
            "the sheet no longer reports its visibility to the booking",
            source("features/booking/BookingBottomSheet.kt").contains("bookingVm.setSheetVisible(visible)"),
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
