package cz.cleansia.customer.features.payments

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/** A missing locale row falls back to English on a screen about money the customer owes. */
class PaymentsCopyTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val keys = listOf(
        "profile_row_payments",
        "payments_title",
        "payments_due_title",
        "payments_due_intro",
        "payments_due_order",
        "payments_pay_action",
        "receivable_kind_cancellation_fee",
        "receivable_kind_lockout",
        "receivable_kind_unpaid_cash",
        "receivable_kind_top_up",
        "receivable_kind_other",
        "payments_card_title",
        "payments_card_intro",
        "payments_card_expires",
        "payments_card_empty",
        "payments_card_remove_action",
        "payments_card_remove_title",
        "payments_card_remove_message",
        "payments_card_remove_confirm",
        "payments_card_removed",
        "payments_error_message",
        "payments_error_retry",
    )

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every payments string is written in all five locales`() {
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

    /** The kind is an ordinal on the wire; its English `name` must never reach the screen. */
    @Test
    fun `the screen labels a debt through the kind resolver and falls back to the generic label`() {
        val screen = File(moduleDir, "src/main/java/cz/cleansia/customer/features/payments/PaymentsScreen.kt").readText()
        assertTrue(
            "the receivable card no longer labels its kind through the resolver",
            screen.contains("receivableKindLabelRes(receivable.kind) ?: R.string.receivable_kind_other"),
        )
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
