package cz.cleansia.customer.features.rewards

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Credit copy states money and a rule about money, so a missing locale falls back to English on a
 * screen about what the customer pays. The share and the expiry date are the server's figures
 * (`GetMyCredit.maxShareOfOrder`, `expiresOn`): no locale may state a number of its own, or the
 * sentence drifts from the rule the first time the rule moves. -> BookingPolicy.MaxCreditShareOfOrder
 */
class CreditCopyStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /** Key → the one placeholder it must carry (null = none). */
    private val keys = mapOf(
        "credit_your_credit" to null,
        "credit_auto_applied_share" to "%1\$d",
        "credit_expires_on" to "%1\$s",
        "credit_none" to null,
        "credit_explainer_title" to null,
        "credit_explainer_source_title" to null,
        "credit_explainer_source_body" to null,
        "credit_explainer_spend_title" to null,
        "credit_explainer_spend_body" to "%1\$d",
        "credit_explainer_points_title" to null,
        "credit_explainer_points_body" to null,
        "profile_row_credit" to null,
        "booking_summary_due_on_card" to null,
        "booking_summary_credit_note" to "%1\$s",
        "booking_summary_credit_card_only" to null,
        "order_paid_with_credit" to null,
        "order_paid_by_card" to null,
    )

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every credit string is written in all five locales`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.keys.forEach { key ->
                assertTrue("$locale/$key is missing or blank", valueOf(xml, key)?.isNotBlank() == true)
            }
        }
    }

    @Test
    fun `the Cyrillic locales are not the English copy`() {
        val english = stringsXml("values")
        listOf("values-uk", "values-ru").forEach { locale ->
            val xml = stringsXml(locale)
            keys.keys.forEach { key ->
                assertTrue("$locale/$key is still English", valueOf(xml, key) != valueOf(english, key))
            }
        }
    }

    /**
     * Owner ruling 2026-10-02: the money balance is "Кредит" in Ukrainian and Russian. "Бонуси/бонусы"
     * read as points, which are a different thing (credit_explainer_points_*), so no credit string may
     * say it, and the labels that name the balance say "кредит".
     */
    @Test
    fun `Ukrainian and Russian call the balance credit, never bonuses`() {
        val named = listOf("credit_your_credit", "profile_row_credit", "credit_explainer_title", "order_paid_with_credit")
        listOf("values-uk", "values-ru").forEach { locale ->
            val xml = stringsXml(locale)
            keys.keys.forEach { key ->
                val value = valueOf(xml, key)!!
                assertTrue("$locale/$key calls credit bonuses: $value", !value.contains("бонус", ignoreCase = true))
            }
            named.forEach { key ->
                assertTrue("$locale/$key does not say кредит", valueOf(xml, key)!!.contains("кредит", ignoreCase = true))
            }
        }
    }

    /**
     * The customer terms of 2026-10-05 (section 9) name the referral reward as a source of credit, and a
     * qualified referral pays the market's credit since the owner ruling of 2026-10-04. The explainer's
     * "where it comes from" names it in the terms' own word, and states no amount (the test above).
     */
    @Test
    fun `the explainer names the referral reward as a source of credit`() {
        val referral = mapOf(
            "values" to "referral reward",
            "values-cs" to "odměna za doporučení",
            "values-sk" to "odmena za odporúčanie",
            "values-uk" to "реферальна винагорода",
            "values-ru" to "реферальное вознаграждение",
        )
        referral.forEach { (locale, word) ->
            val value = valueOf(stringsXml(locale), "credit_explainer_source_body")!!
            assertTrue(
                "$locale/credit_explainer_source_body does not name the $word: $value",
                value.contains(word, ignoreCase = true),
            )
        }
    }

    @Test
    fun `the share and the date are the server's figures, never a number of the copy's own`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keys.forEach { (key, placeholder) ->
                val value = valueOf(xml, key)!!
                if (placeholder != null) {
                    assertTrue("$locale/$key lost its $placeholder placeholder", value.contains(placeholder))
                }
                assertEquals(
                    "$locale/$key names a number of its own",
                    emptyList<Char>(),
                    value.replace("%1\$d", "").replace("%1\$s", "").filter { it.isDigit() }.toList(),
                )
            }
        }
    }

    @Test
    fun `every surface that shows credit renders it`() {
        mapOf(
            "features/booking/ConfirmStep.kt" to listOf(
                "credit_your_credit",
                "booking_summary_due_on_card",
                "booking_summary_credit_note",
                "booking_summary_credit_card_only",
            ),
            "features/orders/OrderDetailSummary.kt" to listOf(
                "order_paid_with_credit",
                "order_paid_by_card",
                "booking_summary_due_on_card",
            ),
            "features/booking/BookingSuccessScreen.kt" to listOf(
                "order_paid_with_credit",
                "order_paid_by_card",
                "booking_summary_due_on_card",
            ),
            "features/profile/ProfileTab.kt" to listOf("profile_row_credit"),
            "features/rewards/RewardsTab.kt" to listOf("credit_none", "credit_auto_applied_share", "credit_explainer_title"),
        ).forEach { (file, rendered) ->
            val source = source(file)
            rendered.forEach { key -> assertTrue("$file no longer renders $key", source.contains("R.string.$key")) }
        }
        // The order detail's card line follows the payment status (PaymentLabelTest). The confirmation
        // opens only after the PaymentSheet completed, so a Pending there is the webhook not in yet: it
        // reads "Paid by card" unless the payment Failed.
        assertTrue(
            "the order detail no longer labels the card share by its payment",
            source("features/orders/OrderDetailSummary.kt").contains("cardShareLabelRes("),
        )
        val success = source("features/booking/BookingSuccessScreen.kt")
        assertTrue(
            "the confirmation labels a card it just charged by a payment status the webhook has not set yet",
            !success.contains("cardShareLabelRes(") && success.contains("order.paymentStatus?.value == 3"),
        )
        assertTrue(
            "the Profile row no longer opens the explainer the Rewards card opens",
            source("features/profile/ProfileTab.kt").contains("CreditExplainerSheet("),
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
