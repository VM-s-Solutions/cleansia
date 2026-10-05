package cz.cleansia.customer.features.home

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * What the home carousel, the profile's Plus card, the recurring paywall, the referral surfaces, the
 * trust badges and the help FAQ promise, held to what the platform delivers. The trial claims are the
 * ones the web's `plus-trial-claim.spec.ts` guards on its own surfaces.
 */
class UpsellClaimTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val solutionDir: File = generateSequence(moduleDir.absoluteFile) { it.parentFile }
        .firstOrNull { File(it, "Cleansia.Api.sln").isFile }
        ?: error("Cleansia.Api.sln not found above ${moduleDir.absolutePath}")

    /**
     * These rows render to a non-member whether or not a trial is on offer, so none may promise one. The
     * trial rows are separate keys, shown only while the customer can still get a trial.
     */
    private val ungatedPlusKeys = listOf(
        "home_upsell_plus_top",
        "home_upsell_plus_title",
        "home_upsell_plus_cta",
        "home_upsell_plus_desc",
        "home_upsell_plus_desc_generic",
        "membership_inactive_badge",
        "membership_inactive_title",
        "membership_inactive_perks_summary",
        "membership_inactive_cta",
        "recurring_plus_gate_title",
        "recurring_plus_gate_subtitle",
        "recurring_plus_gate_cta",
    )

    /** The web spec's stems. A bare "free" is absent: free cancellation is a real, ungated perk. */
    private val trialClaim = Regex(
        "trial|zkušeb|skúšob|пробн|\\btry\\b|vyzkouš|zkuste|vyskúš|skúste|спробу|попроб|" +
            "\\d+\\s*(?:days?|dn|дн)",
        RegexOption.IGNORE_CASE,
    )

    /** Bookings carry cancellation fees, so "cancel anytime" in a perks line reads as a false perk. */
    private val cancelAnytimeClaim = Regex(
        "cancel any\\s?time|kdykoli|kedykoľvek|будь-коли|в любое время|в любой момент",
        RegexOption.IGNORE_CASE,
    )

    /** A referral pays both sides once the friend's first order is completed. */
    private val completedStem = mapOf(
        "values" to "completed",
        "values-cs" to "dokončen",
        "values-sk" to "dokončen",
        "values-uk" to "завершен",
        "values-ru" to "завершённ",
    )

    /** Unused rows that described perks, guarantees or a trial the platform does not offer. */
    private val retiredPromises = listOf(
        "rewards_tier_unlocked",
        "rewards_tier_regular",
        "rewards_tier_regular_perk",
        "rewards_tier_vip",
        "rewards_tier_vip_perk",
        "rewards_tier_hero",
        "rewards_tier_hero_perk",
        "rewards_referral_subtitle",
        "home_hero3_top",
        "home_hero3_title",
        "home_hero3_cta",
        "home_hero_slide3_title",
        "home_hero_slide3_subtitle",
        "home_hero_slide3_cta",
        "membership_inactive_subtitle",
        "membership_trial_badge",
        "membership_trial_disclosure",
        "membership_subscribe_cta",
        "referral_code_valid",
        "home_upsell_welcome_top",
        "home_upsell_welcome_title",
        "home_upsell_welcome_cta",
        "home_trust_vetted",
        "booking_trust_vetted",
        "home_hero_slide1_subtitle",
        "home_trust_insured",
        "booking_trust_insured_no_figure",
        "help_faq_a3_no_figure",
        "home_upsell_chip_points",
        "loyalty_referral_share_text",
    )

    /** No cleaner is background-checked. */
    private val vettingClaim = Regex(
        "vetted|background|prověř|prever|перевірен|перевірк|проверен",
        RegexOption.IGNORE_CASE,
    )

    private val trustKeys = listOf(
        "booking_trust_insured",
        "help_faq_q3",
        "help_faq_a3",
    )

    private val cancelStem = mapOf(
        "values" to "cancel",
        "values-cs" to "zruš",
        "values-sk" to "zruš",
        "values-uk" to "скасу",
        "values-ru" to "отмен",
    )

    @Test
    fun `no ungated Plus row promises a free trial`() {
        val claims = locales.flatMap { locale ->
            val declared = strings(locale)
            ungatedPlusKeys.mapNotNull { key ->
                val value = declared[key] ?: return@mapNotNull "$locale/$key is missing"
                if (trialClaim.containsMatchIn(value)) "$locale/$key: $value" else null
            }
        }
        assertEquals(emptyList<String>(), claims)
    }

    /** The trial length is set per plan in the admin console, so a trial row states the plan's days and no number of its own. */
    private val trialRows = listOf(
        "home_upsell_plus_title_trial",
        "membership_inactive_cta_trial",
        "membership_hero_trial_price",
    )

    @Test
    fun `every trial row states the plan's days rather than a number of its own`() {
        val placeholder = Regex("%\\d+\\$[sd]")
        locales.forEach { locale ->
            val declared = strings(locale)
            trialRows.forEach { key ->
                val value = declared[key] ?: error("$locale/$key is missing")
                assertTrue("$locale/$key does not carry the plan's days — $value", Regex("%\\d+\\\$d").containsMatchIn(value))
                assertTrue("$locale/$key names a number of its own — $value", placeholder.replace(value, "").none { it.isDigit() })
            }
        }
    }

    /** A trialing member has every Plus benefit from day one, so no row may say one waits for a payment. */
    private val waitsForPaymentClaim = mapOf(
        "values" to Regex("first paid|paid membership", RegexOption.IGNORE_CASE),
        "values-cs" to Regex("prvním placen|prvního placen|placené členství|placeného členství", RegexOption.IGNORE_CASE),
        "values-sk" to Regex("prvým platen|prvého platen|platené členstvo|plateného členstva", RegexOption.IGNORE_CASE),
        "values-uk" to Regex("першого оплачен|оплачене членство|платну підписку", RegexOption.IGNORE_CASE),
        "values-ru" to Regex("первого оплаченн|оплаченное членство|платную подписку", RegexOption.IGNORE_CASE),
    )

    @Test
    fun `no row tells a trialing member a benefit waits for the first payment`() {
        val claims = locales.flatMap { locale ->
            val claim = waitsForPaymentClaim.getValue(locale)
            strings(locale).filterValues { claim.containsMatchIn(it) }.map { (key, value) -> "$locale/$key: $value" }
        }
        assertEquals(emptyList<String>(), claims)
    }

    @Test
    fun `the non-member perks line does not promise cancelling anytime`() {
        val claims = locales.mapNotNull { locale ->
            val value = strings(locale)["membership_inactive_perks_summary"]
                ?: return@mapNotNull "$locale/membership_inactive_perks_summary is missing"
            if (cancelAnytimeClaim.containsMatchIn(value)) "$locale: $value" else null
        }
        assertEquals(emptyList<String>(), claims)
    }

    /** A paid-up member's cancel runs to the period end and a past-due one's ends it at once; "anytime" states neither. */
    @Test
    fun `the Plus disclosure and auto-renew hint do not promise cancelling anytime`() {
        val claims = locales.flatMap { locale ->
            val declared = strings(locale)
            listOf("membership_disclosure", "membership_auto_renew_hint").mapNotNull { key ->
                val value = declared[key] ?: return@mapNotNull "$locale/$key is missing"
                if (cancelAnytimeClaim.containsMatchIn(value)) "$locale/$key: $value" else null
            }
        }
        assertEquals(emptyList<String>(), claims)
    }

    /**
     * The rows that state the referral reward → the placeholder the market's credit takes in each, and
     * the twin that renders when the market pays none. The credit is the chosen market currency's
     * `ReferralCredit`, formatted on device; a null or zero figure pays nothing, so the twin promises nothing.
     */
    private val referralRewardRows = mapOf(
        "home_upsell_referral_desc" to ("%1\$s" to "home_upsell_referral_desc_generic"),
        "booking_referral_code_dialog_helper" to ("%1\$s" to "booking_referral_code_dialog_helper_no_figure"),
        "booking_referral_code_dialog_success_named" to ("%2\$s" to "booking_referral_code_dialog_success_named_no_figure"),
        "booking_referral_code_dialog_success" to ("%1\$s" to "booking_referral_code_dialog_success_no_figure"),
        "loyalty_referral_subtitle" to ("%1\$s" to "loyalty_referral_subtitle_no_figure"),
    )

    /** The friend reads the share text in their own market, so it is sent without a figure everywhere. */
    private val referralShareText = "loyalty_referral_share_text_no_figure"

    /**
     * Each side is paid the credit of the currency it books in, so the two figures can differ and a row
     * may state only the reader's own.
     */
    private val sharedFigureClaim = mapOf(
        "values" to "each|both",
        "values-cs" to "oba|obě|každý|každá",
        "values-sk" to "obaja|obe|každý|každá",
        "values-uk" to "обоє|обидва|обидві|обом|кожен|кожна|кожному",
        "values-ru" to "оба|обе|обоим|каждый|каждая|каждому",
    ).mapValues { (_, words) ->
        Regex("(?<!\\p{L})(?:$words)(?!\\p{L})|(?<!\\p{L})по %\\d", RegexOption.IGNORE_CASE)
    }

    private val creditStem = mapOf(
        "values" to "credit",
        "values-cs" to "kredit",
        "values-sk" to "kredit",
        "values-uk" to "кредит",
        "values-ru" to "кредит",
    )

    private val pointsStem = mapOf(
        "values" to Regex("\\bpoints?\\b|\\bpts\\b", RegexOption.IGNORE_CASE),
        "values-cs" to Regex("bod", RegexOption.IGNORE_CASE),
        "values-sk" to Regex("bod", RegexOption.IGNORE_CASE),
        "values-uk" to Regex("бал", RegexOption.IGNORE_CASE),
        "values-ru" to Regex("балл", RegexOption.IGNORE_CASE),
    )

    private val currencyWord = Regex("CZK|Kč|EUR|€|koru|euro|крон|євро|евро", RegexOption.IGNORE_CASE)

    @Test
    fun `every referral reward is the market's credit and waits for the friend's first completed cleaning`() {
        val placeholder = Regex("%\\d+\\$[sd]")
        locales.forEach { locale ->
            val declared = strings(locale)
            referralRewardRows.forEach { (key, row) ->
                val value = declared[key] ?: error("$locale/$key is missing")
                val bare = placeholder.replace(value, "")
                assertTrue("$locale/$key does not take the market's credit as ${row.first} — $value", value.contains(row.first))
                assertTrue("$locale/$key names a number of its own — $value", bare.none { it.isDigit() })
                assertTrue("$locale/$key names a currency of its own — $value", !currencyWord.containsMatchIn(bare))
                assertTrue("$locale/$key does not call the reward credit — $value", value.contains(creditStem.getValue(locale), ignoreCase = true))
                assertTrue("$locale/$key does not wait for a completed cleaning — $value", value.contains(completedStem.getValue(locale), ignoreCase = true))
            }
            val waiting = plurals(locale)["loyalty_referral_stats_waiting"] ?: error("$locale/loyalty_referral_stats_waiting is missing")
            waiting.forEach { item ->
                assertTrue("$locale/loyalty_referral_stats_waiting does not wait for a completed cleaning — $item", item.contains(completedStem.getValue(locale), ignoreCase = true))
            }
        }
    }

    @Test
    fun `every referral reward row promises the reader only their own figure`() {
        val claims = locales.flatMap { locale ->
            val declared = strings(locale)
            val claim = sharedFigureClaim.getValue(locale)
            referralRewardRows.keys.mapNotNull { key ->
                val value = declared[key] ?: return@mapNotNull "$locale/$key is missing"
                if (claim.containsMatchIn(value)) "$locale/$key: $value" else null
            }
        }
        assertEquals(emptyList<String>(), claims)
    }

    @Test
    fun `a market that pays no referral credit is promised none`() {
        val placeholder = Regex("%\\d+\\$[sd]")
        locales.forEach { locale ->
            val declared = strings(locale)
            (referralRewardRows.values.map { it.second } + referralShareText).forEach { twin ->
                val value = declared[twin] ?: error("$locale/$twin is missing")
                assertTrue("$locale/$twin names a figure — $value", placeholder.replace(value, "").none { it.isDigit() })
                assertTrue("$locale/$twin promises credit — $value", !value.contains(creditStem.getValue(locale), ignoreCase = true))
                assertTrue("$locale/$twin promises points — $value", !pointsStem.getValue(locale).containsMatchIn(value))
            }
        }
    }

    /** The points ledger's own line and the code field's example are the referral rows outside the reward. */
    private val referralRowsOutsideTheReward = setOf("loyalty_tx_referral", "referral_code_field_placeholder")

    @Test
    fun `no referral row still promises points or states a figure of its own`() {
        val placeholder = Regex("%\\d+\\$[sd]")
        locales.forEach { locale ->
            val rows = strings(locale).filterKeys { "referral" in it && it !in referralRowsOutsideTheReward } +
                plurals(locale).filterKeys { "referral" in it }.mapValues { it.value.joinToString(" | ") }
            rows.forEach { (key, value) ->
                assertTrue("$locale/$key names a number of its own — $value", placeholder.replace(value, "").none { it.isDigit() })
                assertTrue("$locale/$key still promises points — $value", !pointsStem.getValue(locale).containsMatchIn(value))
            }
            assertTrue("$locale/home_upsell_referral_desc is still a points plural", "home_upsell_referral_desc" !in plurals(locale))
        }
    }

    @Test
    fun `no trust badge or FAQ answer claims the cleaners are vetted`() {
        val claims = locales.flatMap { locale ->
            val declared = strings(locale)
            trustKeys.mapNotNull { key ->
                val value = declared[key] ?: return@mapNotNull "$locale/$key is missing"
                if (vettingClaim.containsMatchIn(value)) "$locale/$key: $value" else null
            }
        }
        assertEquals(emptyList<String>(), claims)
    }

    /** No booking can be moved: the customer cancels, free while no cleaner has taken it, and books again. */
    @Test
    fun `the FAQ promises no rescheduling and points to cancelling`() {
        locales.forEach { locale ->
            val answer = strings(locale)["help_faq_a4"] ?: error("$locale/help_faq_a4 is missing")
            assertTrue("$locale/help_faq_a4 states a figure — $answer", answer.none { it.isDigit() })
            assertTrue("$locale/help_faq_a4 names a button to tap — $answer", !answer.contains("\\\""))
            assertTrue("$locale/help_faq_a4 does not point to cancelling — $answer", answer.contains(cancelStem.getValue(locale), ignoreCase = true))
        }
    }

    @Test
    fun `the retired promises stay deleted in every locale`() {
        val back = locales.flatMap { locale ->
            val declared = strings(locale)
            retiredPromises.filter { it in declared }.map { "$locale/$it" }
        }
        assertEquals(emptyList<String>(), back)
    }

    /** The newer carousel slides; every one renders, so every one is in every locale. */
    private val carouselKeys = listOf(
        "home_upsell_notifications_top",
        "home_upsell_notifications_title",
        "home_upsell_notifications_cta",
        "home_upsell_credit_title",
        "home_upsell_express_top",
        "home_upsell_book_cta",
        "home_upsell_did_you_know",
        "home_upsell_notifications_desc",
        "home_upsell_credit_desc",
        "home_upsell_express_desc",
        "home_upsell_setup_recurring_desc",
        "home_upsell_plus_desc",
        "home_upsell_plus_desc_generic",
        "home_upsell_referral_desc_generic",
        "home_upsell_plus_cancel_title",
        "home_upsell_plus_cancel_desc",
        "home_upsell_express_today_title",
        "home_upsell_express_today_desc",
        "home_upsell_rewards_title",
        "home_upsell_rewards_desc",
        "home_upsell_rewards_cta",
        "home_upsell_times_title",
        "home_upsell_times_desc",
        "home_quick_size_title",
        "home_quick_size_cta",
        "home_quick_size_rooms_less",
        "home_quick_size_rooms_more",
        "home_quick_size_baths_less",
        "home_quick_size_baths_more",
    )

    /** Key → the placeholders it must carry; it may state no number of its own. */
    private val carouselFigures = mapOf(
        "home_upsell_credit_title" to listOf("%1\$s"),
        "home_upsell_credit_desc" to listOf("%1\$d"),
        "home_upsell_express_top" to listOf("%1\$d", "%2\$d"),
        "home_upsell_plus_desc" to listOf("%1\$d"),
        "home_upsell_plus_cancel_title" to listOf("%1\$d"),
        "home_upsell_express_today_title" to listOf("%1\$d"),
        "home_upsell_express_today_desc" to listOf("%1\$d"),
        "home_upsell_times_desc" to listOf("%1\$s", "%2\$s"),
        "home_upsell_chip_percent_off" to listOf("%1\$d"),
        "home_upsell_chip_credit" to listOf("%1\$s"),
        "home_upsell_chip_hours" to listOf("%1\$d"),
        "home_upsell_chip_minutes" to listOf("%1\$d"),
        "home_upsell_chip_times" to listOf("%1\$d"),
    )

    /** The rows that state no figure at all: their facts are the server's or the booking's, carried elsewhere. */
    private val carouselFigureFree = listOf(
        "home_upsell_did_you_know",
        "home_upsell_notifications_desc",
        "home_upsell_express_desc",
        "home_upsell_setup_recurring_title",
        "home_upsell_setup_recurring_desc",
        "home_upsell_plus_desc_generic",
        "home_upsell_plus_cancel_desc",
        "home_upsell_rewards_title",
        "home_upsell_rewards_desc",
        "home_upsell_times_title",
    )

    @Test
    fun `the did-you-know rows without a placeholder state no figure`() {
        locales.forEach { locale ->
            val declared = strings(locale)
            carouselFigureFree.forEach { key ->
                val value = declared[key] ?: error("$locale/$key is missing")
                assertTrue("$locale/$key names a figure of its own — $value", value.none { it.isDigit() })
            }
        }
    }

    @Test
    fun `every new carousel slide is written in all five locales and promises no trial`() {
        val english = strings("values")
        locales.forEach { locale ->
            val declared = strings(locale)
            carouselKeys.forEach { key ->
                val value = declared[key]
                assertTrue("$locale/$key is missing or blank", value?.isNotBlank() == true)
                assertTrue("$locale/$key promises a trial — $value", !trialClaim.containsMatchIn(value!!))
                if (locale == "values-uk" || locale == "values-ru") {
                    assertTrue("$locale/$key is still English", value != english[key])
                }
            }
            val express = plurals(locale)["home_upsell_express_title"]
            assertTrue("$locale/home_upsell_express_title is missing", !express.isNullOrEmpty())
        }
    }

    /**
     * The credit balance and its share are the server's (`GetMyCredit`), the waivers left, the
     * discount and the cancellation window are the membership's, and the express window and the
     * arrival times are the booking policy's — so no locale states a number.
     */
    @Test
    fun `the credit and express slides state the server's figures, never their own`() {
        val placeholder = Regex("%\\d+\\$[sd]")
        locales.forEach { locale ->
            val declared = strings(locale)
            carouselFigures.forEach { (key, required) ->
                val value = declared[key] ?: error("$locale/$key is missing")
                required.forEach { assertTrue("$locale/$key lost its $it placeholder — $value", value.contains(it)) }
                assertTrue("$locale/$key names a number of its own — $value", placeholder.replace(value, "").none { it.isDigit() })
            }
            plurals(locale).getValue("home_upsell_express_title").forEach { item ->
                assertTrue("$locale/home_upsell_express_title lost its count — $item", item.contains("%1\$d"))
                assertTrue("$locale/home_upsell_express_title names a number of its own — $item", placeholder.replace(item, "").none { it.isDigit() })
            }
        }
    }

    /** The quick-size slide showed "2 bath": English `other` had been left on the singular. */
    @Test
    fun `the size steppers count rooms and baths in the English plural`() {
        val english = plurals("values")
        assertEquals(listOf("%1\$d room", "%1\$d rooms"), english["booking_rooms_short"])
        assertEquals(listOf("%1\$d bath", "%1\$d baths"), english["booking_bath_short"])
    }

    /** The express slide states the 2–4 h window from the client's bands, so those must be the server's. */
    @Test
    fun `the express window the slide states is the booking policy's`() {
        val policy = File(solutionDir, "Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs").readText()
        fun hours(name: String): Int = Regex("public\\s+const\\s+int\\s+$name\\s*=\\s*(\\d+)\\s*;")
            .find(policy)?.groupValues?.get(1)?.toInt()
            ?: error("BookingPolicy.$name not found — the parser needs updating")
        assertEquals(hours("ExpressLeadTimeHours"), cz.cleansia.customer.features.booking.BookingPricing.EXPRESS_LEAD_HOURS.toInt())
        assertEquals(hours("StandardLeadTimeHours"), cz.cleansia.customer.features.booking.BookingPricing.STANDARD_LEAD_HOURS.toInt())
    }

    private fun strings(locale: String): Map<String, String> {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<string name=\"([^\"]+)\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .findAll(file.readText())
            .associate { it.groupValues[1] to it.groupValues[2] }
    }

    private fun plurals(locale: String): Map<String, List<String>> {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<plurals name=\"([^\"]+)\">(.*?)</plurals>", RegexOption.DOT_MATCHES_ALL)
            .findAll(file.readText())
            .associate { match ->
                match.groupValues[1] to Regex("<item quantity=\"[^\"]+\">(.*?)</item>", RegexOption.DOT_MATCHES_ALL)
                    .findAll(match.groupValues[2])
                    .map { it.groupValues[1] }
                    .toList()
            }
    }
}
