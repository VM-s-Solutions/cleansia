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
        "booking_card_guarantee_title",
        "booking_card_guarantee_body",
        "booking_card_guarantee_consent_required",
        "booking_card_guarantee_pending",
        "booking_card_guarantee_cancelled",
    )

    private val crewCounts = listOf("booking_cash_needs_card", "recurring_cash_needs_card")

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val solutionDir: File = generateSequence(moduleDir.absoluteFile) { it.parentFile }
        .firstOrNull { File(it, "Cleansia.Api.sln").isFile }
        ?: error("Cleansia.Api.sln not found above ${moduleDir.absolutePath}")

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

    /**
     * The server stamps every saved card with the version of the consent wording in force, so the text
     * the review step shows is the resource named for that version: bumping the version on the server
     * fails here until the new wording exists in all five locales and is the one rendered.
     */
    @Test
    fun `the card-guarantee consent shown is the wording of the version the server records`() {
        val savedCard = File(solutionDir, "Cleansia.Core.Domain/Users/SavedCard.cs").readText()
        val version = Regex("ConsentTextVersionInForce\\s*=\\s*\"([^\"]+)\"").find(savedCard)?.groupValues?.get(1)
            ?: error("SavedCard.ConsentTextVersionInForce not found — the parser needs updating")
        val key = "consent_" + version.replace('-', '_')

        locales.forEach { locale ->
            assertTrue("$locale/$key is missing or blank", valueOf(stringsXml(locale), key)?.isNotBlank() == true)
        }
        listOf("values-uk", "values-ru").forEach { locale ->
            assertTrue("$locale/$key is still English", valueOf(stringsXml(locale), key) != valueOf(stringsXml("values"), key))
        }
        assertTrue("the review step no longer shows $key", source("features/booking/ConfirmStep.kt").contains("R.string.$key"))
    }

    @Test
    fun `the first cash booking saves the card in PaymentSheet's setup mode and books once it lands`() {
        val sheet = source("features/booking/BookingBottomSheet.kt")
        assertTrue("the guarantee no longer opens PaymentSheet in setup mode", sheet.contains("cardGuaranteeSheet.presentWithSetupIntent("))
        assertTrue("a saved card no longer books the cash order", sheet.contains("bookingVm.submitAfterCardGuarantee()"))
        assertEquals(
            "a cancelled or failed setup sheet no longer lets the next swipe capture afresh",
            2,
            Regex("bookingVm\\.abandonCardGuarantee\\(\\)").findAll(sheet).count(),
        )
        assertTrue(
            "the consent is no longer shown when a card is needed",
            source("features/booking/ConfirmStep.kt").contains("if (needsCardGuarantee)"),
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
